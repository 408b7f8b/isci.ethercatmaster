﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;

namespace isci.ethercatmaster
{
    public class Konfiguration : Parameter
    {
        [fromArgs, fromEnv]
        public string interfaceName;
        [fromArgs, fromEnv]
        public string pfadESI;
        [fromArgs, fromEnv]
        public uint zykluszeitIO = 20;
        public Konfiguration(string[] args) : base(args) {
            /* var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            pfadESI = System.IO.Path.Combine(localAppDataPath, "ESI"); */

            isci.Helfer.OrdnerPruefenErstellen(pfadESI);
        }
    }

    class Program
    {
        static EtherCAT.NET.EcMaster master;

        static void UpdateIO(object state)
        {
            master.UpdateIO(DateTime.Now);
        }

        static void Main(string[] args)
        {
            var konfiguration = new Konfiguration(args);
            
            var structure = new Datenstruktur(konfiguration);
            var ausfuehrungsmodell = new Ausführungsmodell(konfiguration, structure.Zustand);

            var dm = new Datenmodell(konfiguration.Identifikation);

            var settings = new EtherCAT.NET.EcSettings(cycleFrequency: 10U, konfiguration.pfadESI, konfiguration.interfaceName);

            /* scan available slaves */
            var rootSlave = EtherCAT.NET.EcUtilities.ScanDevices(settings.InterfaceName);

            var slaves = rootSlave.Descendants().ToList();

            slaves.ForEach(slave => 
            {
                EtherCAT.NET.EcUtilities.CreateDynamicData(settings.EsiDirectoryPath, slave);
            });

            Console.WriteLine($"Found {slaves.Count()} slaves:");

            foreach (var slave in slaves)
            {
                Console.WriteLine($"{slave.DynamicData.Name} (PDOs: {slave.DynamicData.Pdos.Count} - CSA: {slave.Csa})");
            }

            settings.TargetTimeDifference = 1000; //in Nanosekunden

            master = new EtherCAT.NET.EcMaster(settings);
            
            try
            {
                master.Configure(rootSlave);
            }
            catch (Exception ex)
            {
                throw;
            }

            master.UpdateIO(DateTime.UtcNow);

            var variables = slaves.SelectMany(child => child.GetVariables()).ToList();

            var eingangsvariablen = new List<KeyValuePair<Dateneintrag, EtherCAT.NET.Infrastructure.SlaveVariable>>();
            var ausgangsvariablen = new List<KeyValuePair<Dateneintrag, EtherCAT.NET.Infrastructure.SlaveVariable>>();
            

            foreach (var variable in variables)
            {
                unsafe
                {
                    switch(variable.DataType)
                    {
                        case EtherCAT.NET.Infrastructure.EthercatDataType.Boolean:
                        {
                            var zugeordneterSlaveCsa = variable.Parent.Parent.Csa;
                            var zugeordneterSlave = slaves.FirstOrDefault(current => current.Csa == zugeordneterSlaveCsa);
                            var zugeordneterSlaveIndex = slaves.IndexOf(zugeordneterSlave);

                            var wort = new Span<int>(variable.DataPtr.ToPointer(), 1);
                            var wert = ((wort[0] >> variable.BitOffset) & 1) != 0;

                            var eintrag = new dtBool(wert, "Slave" + zugeordneterSlaveIndex + "_" + zugeordneterSlave.DynamicData.Name + "_" + variable.Parent.Name.Replace(" ", "") + "_" + variable.Name);
                            dm.Dateneinträge.Add(eintrag);

                            if (variable.DataDirection == EtherCAT.NET.Infrastructure.DataDirection.Input) eingangsvariablen.Add(new KeyValuePair<Dateneintrag, EtherCAT.NET.Infrastructure.SlaveVariable>(eintrag, variable));
                            if (variable.DataDirection == EtherCAT.NET.Infrastructure.DataDirection.Output) ausgangsvariablen.Add(new KeyValuePair<Dateneintrag, EtherCAT.NET.Infrastructure.SlaveVariable>(eintrag, variable));

                            break;
                        }
                    }
                }
            }

            dm.Speichern(konfiguration.OrdnerDatenmodelle + "/" + konfiguration.Identifikation + ".json");

            var beschreibung = new Modul(konfiguration.Identifikation, "isci.ethercatmaster", dm.Dateneinträge);
            beschreibung.Name = "EtherCAT-Master Ressource " + konfiguration.Identifikation;
            beschreibung.Beschreibung = "Ethercat-Master";
            beschreibung.Speichern(konfiguration.OrdnerBeschreibungen + "/" + konfiguration.Identifikation + ".json");

            structure.DatenmodellEinhängen(dm);
            structure.DatenmodelleEinhängenAusOrdner(konfiguration.OrdnerDatenmodelle);
            structure.Start();

            var timer = new System.Threading.Timer(UpdateIO, null, 0, konfiguration.zykluszeitIO);            
            
            while(true)
            {
                structure.Zustand.WertAusSpeicherLesen();

                if (ausfuehrungsmodell.AktuellerZustandModulAktivieren())
                {
                    var zustandParameter = ausfuehrungsmodell.ParameterAktuellerZustand();

                    switch (zustandParameter)
                    {
                        case "E":
                        {
                            unsafe
                            {
                                foreach (var eingang in eingangsvariablen)
                                {
                                    var wort = new Span<int>(eingang.Value.DataPtr.ToPointer(), 1);
                                    var wert = ((wort[0] >> eingang.Value.BitOffset) & 1) != 0;

                                    if ((bool)eingang.Key.Wert == wert) continue;

                                    eingang.Key.Wert = wert;
                                    eingang.Key.WertInSpeicherSchreiben();
                                }
                            }
                            //structure.Schreiben();
                            break;
                        }
                        case "A":
                        {
                            structure.Lesen();

                            unsafe
                            {
                                foreach (var ausgang in ausgangsvariablen)
                                {
                                    if (ausgang.Key.aenderungExtern)
                                    {
                                        var ziel = new Span<int>(ausgang.Value.DataPtr.ToPointer(), 1); //Pointer auf den Speicherbereich
                                        var bitwert = ((dtBool)ausgang.Key).Wert ? 1 : 0; // 1 oder 0 schreiben?

                                        int mask = 1 << ausgang.Value.BitOffset; //Maske aufbauen mit dem betroffenen Bit des Ausgangs
                                        ziel[0] = ziel[0] & ~mask; //Aktuelles Bit des Ausgangs nullsetzen

                                        ziel[0] |= bitwert << ausgang.Value.BitOffset; //ODER-Operation zwischen Speicherbereich und dem neuen Wert für den Ausgang
                                        ausgang.Key.aenderungExtern = false;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    ausfuehrungsmodell.Folgezustand();
                    structure.Zustand.WertInSpeicherSchreiben();
                }

                //isci.Helfer.SleepForMicroseconds(konfiguration.PauseArbeitsschleifeUs);
                System.Threading.Thread.Sleep(1);
            }
        }
    }
}