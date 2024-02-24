using System;
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

            master = new EtherCAT.NET.EcMaster(settings);

            settings.TargetTimeDifference = 1000; //in Nanosekunden
            
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

                            var wert = new Span<bool>(variable.DataPtr.ToPointer(), 1);

                            
                            var eintrag = new dtBool(wert[0], "Slave" + zugeordneterSlaveIndex + "_" + zugeordneterSlave.DynamicData.Name + "_" + variable.Parent.Name.Replace(" ", "") + "_" + variable.Name);
                            dm.Dateneinträge.Add(eintrag);

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

            var timer = new System.Threading.Timer(UpdateIO, null, 0, 50);

            
            
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
                                if (variables.Any())
                                {
                                    //var myVariableSpan = new Span<int>(variables.First().DataPtr.ToPointer(), 1);
                                    //myVariableSpan[0] = random.Next(0, 100);
                                }
                            }
                            break;
                        }
                        case "A":
                        {
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