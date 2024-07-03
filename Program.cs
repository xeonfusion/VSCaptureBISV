/*
 * This file is part of VitalSignsCaptureBISV v1.003.
 * Copyright (C) 2024 John George K., xeonfusion@users.sourceforge.net

    VitalSignsCaptureBISV is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    VitalSignsCaptureBISV is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with VitalSignsCaptureBISV.  If not, see <http://www.gnu.org/licenses/>.*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

namespace VSCaptureBISV
{
    class ProgramBISV
    {
        static EventHandler dataEvent;
        public static string DeviceID;
        public static string JSONPostUrl;
        public static string MQTTUrl;
        public static string MQTTtopic;
        public static string MQTTuser;
        public static string MQTTpassw;

        public static void Main(string[] args)
        {
            Console.WriteLine("VitalSignsCaptureBISV v1.003 (C)2024 John George K.");
            Console.WriteLine("For command line usage: -help");
            Console.WriteLine();

            // Create a new SerialPort object with default settings.
            BSerialPort _serialPort = BSerialPort.getInstance;
            string portName;
            string sIntervalset;
            string sWaveformSet;
            string sSpectralset;

            var parser = new CommandLineParser();
            parser.Parse(args);

            if (parser.Arguments.ContainsKey("help"))
            {
                Console.WriteLine("VSCaptureBISV.exe -port [portname] -interval [number]");
                Console.WriteLine(" -export[number] -devid[name] -url [name] -waveset[number] - scale[number]");
                Console.WriteLine("-port <Set serial port name>");
                Console.WriteLine("-interval <Set numeric transmission interval>");
                Console.WriteLine("-export <Set data export CSV, MQTT or JSON option>");
                Console.WriteLine("-devid <Set device ID for MQTT or JSON export>");
                Console.WriteLine("-url <Set MQTT or JSON export url>");
                Console.WriteLine("-topic <Set topic for MQTT export>");
                Console.WriteLine("-user <Set username for MQTT export>");
                Console.WriteLine("-passw <Set password for MQTT export>");
                Console.WriteLine("-waveset <Set waveform export option>");
                Console.WriteLine("-scale <Set waveform ADC or calibrated export option>");
                Console.WriteLine();
                return;
            }

            if (parser.Arguments.ContainsKey("port"))
            {
                portName = parser.Arguments["port"][0];
            }
            else
            {
                Console.WriteLine("Select the Port to which Covidien BIS Vista monitor (Vista Binary protocol) is to be connected, Available Ports:");
                foreach (string s in SerialPort.GetPortNames())
                {
                    Console.WriteLine(" {0}", s);
                }


                Console.Write("COM port({0}): ", _serialPort.PortName.ToString());
                portName = Console.ReadLine();

            }

            if (portName != "")
            {
                // Allow the user to set the appropriate properties.
                _serialPort.PortName = portName;
            }

            try
            {
                _serialPort.Open();

                if (_serialPort.OSIsUnix())
                {
                    dataEvent += new EventHandler((object sender, EventArgs e) => ReadData(sender));
                }

                if (!_serialPort.OSIsUnix())
                {
                    _serialPort.DataReceived += new SerialDataReceivedEventHandler(p_DataReceived);
                }

                if (parser.Arguments.ContainsKey("interval"))
                {
                    sIntervalset = parser.Arguments["interval"][0];
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Numeric Data Transmission sets:");
                    Console.WriteLine("1. 1 second");
                    Console.WriteLine("2. 5 second");
                    Console.WriteLine("3. 1 minute");
                    Console.WriteLine("4. 5 minute");
                    Console.WriteLine("5. Single poll");
                    Console.WriteLine();
                    Console.Write("Choose Data Transmission interval (1-5):");

                    sIntervalset = Console.ReadLine();

                }

                int[] setarray = { 1, 5, 60, 300, 0 };
                short nIntervalset = 2;
                int nInterval = 5;
                if (sIntervalset != "") nIntervalset = Convert.ToInt16(sIntervalset);
                if (nIntervalset > 0 && nIntervalset < 6) nInterval = setarray[nIntervalset - 1];

                if (parser.Arguments.ContainsKey("spectral"))
                {
                    sSpectralset = parser.Arguments["spectral"][0];
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Spectral Data options:");
                    Console.WriteLine("1. Disabled");
                    Console.WriteLine("2. Enabled");
                    Console.WriteLine();
                    Console.Write("Choose spectral data option (1-2):");

                    sSpectralset = Console.ReadLine();
                }
                short nSpectralSet = 1;
                if (sSpectralset != "") nSpectralSet = Convert.ToInt16(sSpectralset);

                if (nSpectralSet == 1) _serialPort.m_spectraldataenable = false;
                if (nSpectralSet == 2) _serialPort.m_spectraldataenable = true;


                string sDataExportset;
                if (parser.Arguments.ContainsKey("export"))
                {
                    sDataExportset = parser.Arguments["export"][0];
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Data export options:");
                    Console.WriteLine("1. Export as CSV files");
                    Console.WriteLine("2. Export as CSV files and JSON to URL");
                    Console.WriteLine("3. Export as MQTT to URL");
                    Console.WriteLine("4. Export as JSON file");
                    Console.WriteLine();
                    Console.Write("Choose data export option (1-4):");

                    sDataExportset = Console.ReadLine();

                }

                int nDataExportset = 1;
                if (sDataExportset != "") nDataExportset = Convert.ToInt32(sDataExportset);

                if (nDataExportset == 2)
                {
                    if (parser.Arguments.ContainsKey("devid"))
                    {
                        DeviceID = parser.Arguments["devid"][0];
                    }
                    else
                    {
                        Console.Write("Enter Device ID/Name:");
                        DeviceID = Console.ReadLine();

                    }

                    if (parser.Arguments.ContainsKey("url"))
                    {
                        JSONPostUrl = parser.Arguments["url"][0];
                    }
                    else
                    {
                        Console.Write("Enter JSON Data Export URL(http://):");
                        JSONPostUrl = Console.ReadLine();

                    }
                }

                if (nDataExportset == 3)
                {
                    if (parser.Arguments.ContainsKey("devid"))
                    {
                        DeviceID = parser.Arguments["devid"][0];
                    }
                    else
                    {
                        Console.Write("Enter Device ID/Name:");
                        DeviceID = Console.ReadLine();

                    }

                    if (parser.Arguments.ContainsKey("url"))
                    {
                        MQTTUrl = parser.Arguments["url"][0];
                    }
                    else
                    {
                        Console.Write("Enter MQTT WebSocket Server URL(ws://):");
                        MQTTUrl = Console.ReadLine();

                    }

                    if (parser.Arguments.ContainsKey("topic"))
                    {
                        MQTTtopic = parser.Arguments["topic"][0];
                    }
                    else
                    {
                        Console.Write("Enter MQTT Topic:");
                        MQTTtopic = Console.ReadLine();

                    }

                    if (parser.Arguments.ContainsKey("user"))
                    {
                        MQTTuser = parser.Arguments["user"][0];
                    }
                    else
                    {
                        Console.Write("Enter MQTT Username:");
                        MQTTuser = Console.ReadLine();

                    }

                    if (parser.Arguments.ContainsKey("passw"))
                    {
                        MQTTpassw = parser.Arguments["passw"][0];
                    }
                    else
                    {
                        Console.Write("Enter MQTT Password:");
                        MQTTpassw = Console.ReadLine();

                    }

                }

                _serialPort.m_DeviceID = DeviceID;
                _serialPort.m_jsonposturl = JSONPostUrl;
                _serialPort.m_MQTTUrl = MQTTUrl;
                _serialPort.m_MQTTtopic = MQTTtopic;
                _serialPort.m_MQTTuser = MQTTuser;
                _serialPort.m_MQTTpassw = MQTTpassw;

                if (nDataExportset > 0 && nDataExportset < 5) _serialPort.m_dataexportset = nDataExportset;

                if (parser.Arguments.ContainsKey("waveset"))
                {
                    sWaveformSet = parser.Arguments["waveset"][0];
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Waveform data Transmission sets:");
                    Console.WriteLine("1. None");
                    Console.WriteLine("2. EEG1, EEG2");
                    Console.WriteLine();
                    Console.Write("Choose Waveform data Transmission set (1-2):");

                    sWaveformSet = Console.ReadLine();

                }

                short nWaveformSet = 2;
                if (sWaveformSet != "") nWaveformSet = Convert.ToInt16(sWaveformSet);

                string sWavescaleSet;
                if (parser.Arguments.ContainsKey("scale"))
                {
                    sWavescaleSet = parser.Arguments["scale"][0];
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Waveform data export scale and calibrate options:");
                    Console.WriteLine("1. Export scaled ADC values");
                    Console.WriteLine("2. Export calibrated values");
                    Console.WriteLine();
                    Console.Write("Choose Waveform data export scale option (1-2):");

                    sWavescaleSet = Console.ReadLine();

                }

                short nWavescaleSet = 2;
                if (sWavescaleSet != "") nWavescaleSet = Convert.ToInt16(sWavescaleSet);

                if (nWavescaleSet == 1) _serialPort.m_calibratewavevalues = false;
                if (nWavescaleSet == 2) _serialPort.m_calibratewavevalues = true;


                Console.WriteLine();
                Console.WriteLine("Requesting Transmission set {0} from monitor", nIntervalset);


                Console.WriteLine();
                Console.WriteLine("Data will be written to CSV file BISVExportData.csv in same folder");

                //_serialPort.RequestStatus();
                //WaitForMilliSeconds(200);

                Task.Run(() => _serialPort.SendCycledRequests(nInterval));

                if (nWaveformSet != 1)
                {
                    Task.Run(() => _serialPort.SendCycledWaveRequests(nInterval));
                }

                Console.WriteLine("Press Escape button to Stop");

                if (_serialPort.OSIsUnix())
                {
                    do
                    {
                        if (_serialPort.BytesToRead != 0)
                        {
                            dataEvent.Invoke(_serialPort, new EventArgs());
                        }

                        if (Console.KeyAvailable == true)
                        {
                            if (Console.ReadKey(true).Key == ConsoleKey.Escape) break;
                        }
                    }
                    while (Console.KeyAvailable == false);

                }

                if (!_serialPort.OSIsUnix())
                {
                    ConsoleKeyInfo cki;

                    do
                    {
                        cki = Console.ReadKey(true);
                    }
                    while (cki.Key != ConsoleKey.Escape);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening/writing to serial port :: " + ex.Message, "Error!");
            }
            finally
            {
                _serialPort.StopTransfer();

                _serialPort.Close();

            }


        }

        static void p_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            ReadData(sender);

        }

        public static void ReadData(object sender)
        {
            try
            {
                (sender as BSerialPort).ReadBuffer();

            }
            catch (TimeoutException) { }
        }

        public static void WaitForMilliSeconds(int nmillisec)
        {
            DateTime dt = DateTime.Now;
            DateTime dt2 = dt.AddMilliseconds(nmillisec);
            do
            {
                dt = DateTime.Now;
            }
            while (dt2 > dt);

        }
    }

    public class CommandLineParser
    {
        public CommandLineParser()
        {
            Arguments = new Dictionary<string, string[]>();
        }

        public IDictionary<string, string[]> Arguments { get; private set; }

        public void Parse(string[] args)
        {
            string currentName = "";
            var values = new List<string>();
            foreach (string arg in args)
            {
                if (arg.StartsWith("-", StringComparison.InvariantCulture))
                {
                    if (currentName != "" && values.Count != 0)
                        Arguments[currentName] = values.ToArray();

                    else
                    {
                        values.Add("");
                        Arguments[currentName] = values.ToArray();
                    }
                    values.Clear();
                    currentName = arg.Substring(1);
                }
                else if (currentName == "")
                    Arguments[arg] = new string[0];
                else
                    values.Add(arg);
            }

            if (currentName != "")
                Arguments[currentName] = values.ToArray();
        }

        public bool Contains(string name)
        {
            return Arguments.ContainsKey(name);
        }
    }

}
