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
        public string interfaceName;            
        public string pfadESI;
        public Konfiguration(string datei) : base(datei) {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            pfadESI = System.IO.Path.Combine(localAppDataPath, "ESI");

            isci.Helfer.OrdnerPruefenErstellen(pfadESI);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var konfiguration = new Konfiguration("konfiguration.json");
            
            var structure = new Datenstruktur(konfiguration.OrdnerDatenstruktur);

            var dm = new Datenmodell(konfiguration.Identifikation);

            var settings = new EtherCAT.NET.EcSettings(cycleFrequency: 10U, konfiguration.pfadESI, konfiguration.interfaceName);

            /* scan available slaves */
            var rootSlave = EtherCAT.NET.EcUtilities.ScanDevices(settings.InterfaceName);

            var message = new System.Text.StringBuilder();
            var slaves = rootSlave.Descendants().ToList();

            slaves.ForEach(slave => 
            {
                EtherCAT.NET.EcUtilities.CreateDynamicData(settings.EsiDirectoryPath, slave);
            });

            message.AppendLine($"Found {slaves.Count()} slaves:");

            foreach (var slave in slaves)
            {
                message.AppendLine($"{slave.DynamicData.Name} (PDOs: {slave.DynamicData.Pdos.Count} - CSA: {slave.Csa})");
            }

            var variables = slaves.SelectMany(child => child.GetVariables()).ToList();

            foreach (var variable in variables)
            {
                unsafe
                {
                    switch(variable.DataType)
                    {
                        case EtherCAT.NET.Infrastructure.EthercatDataType.Boolean:
                        {
                            
                            var eintrag = new dtBool((new Span<bool>(variable.DataPtr.ToPointer(), 1))[0], variable.Name);
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

            var Zustand = new dtZustand(konfiguration.OrdnerDatenstruktur);
            Zustand.Start();

            var master = new EtherCAT.NET.EcMaster(settings);
            
            try
            {
                master.Configure(rootSlave);
            }
            catch (Exception ex)
            {
                throw;
            }
            
            while(true)
            {
                Zustand.Lesen();

                var erfüllteTransitionen = konfiguration.Ausführungstransitionen.Where(a => a.Eingangszustand == (System.Int32)Zustand.value);
                if (erfüllteTransitionen.Count<Ausführungstransition>() <= 0) continue;

                master.UpdateIO(DateTime.UtcNow);

                unsafe
                {
                    if (variables.Any())
                    {
                        //var myVariableSpan = new Span<int>(variables.First().DataPtr.ToPointer(), 1);
                        //myVariableSpan[0] = random.Next(0, 100);
                    }
                }

                structure.Schreiben();

                Zustand.value = erfüllteTransitionen.First<Ausführungstransition>().Ausgangszustand;
                Zustand.Schreiben();
            }
        }
    }
}