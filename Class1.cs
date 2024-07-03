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
using System.IO.Ports;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Net;
using System.Text.Json;

namespace VSCaptureBISV
{
    public class Crc
    {
        public ushort ComputeChecksum(byte[] bytes)
        {
            ushort sum = 0;
            ushort crc = 0;

            for (int i = 0; i < bytes.Length; ++i)
            {
                sum += bytes[i];
            }

            //get 16-bit sum total checksum
            crc = (ushort)(sum & 0xFFFF);                  //For BIS Vista
            return crc;
        }
    }

    public sealed class BSerialPort : SerialPort
    {
        private int BPortBufSize;
        public byte[] BPort_rxbuf;
        public List<byte[]> FrameList = new List<byte[]>();
        public List<byte> m_BufferByteList = new List<byte>();
        public List<byte> m_ResponseByteList = new List<byte>();
        public string m_strTimestamp;
        public byte m_parametertype;
        private bool m_transmissionstart = true;
        public bool m_transmissionstart2 = true;

        public List<NumericValResult> m_NumericValList = new List<NumericValResult>();
        public List<string> m_NumValHeaders = new List<string>();
        public StringBuilder m_strbuildvalues = new StringBuilder();
        public StringBuilder m_strbuildheaders = new StringBuilder();

        public List<WaveValResult> m_WaveValResultList = new List<WaveValResult>();
        public StringBuilder m_strbuildwavevalues = new StringBuilder();
        public StringBuilder m_strbuildwavevalues2 = new StringBuilder();
        public double m_RealtiveTimeCounter = 0;

        public dsc_info_struct m_Dsc_Info_Struct = new dsc_info_struct();
        public bool m_calibratewavevalues = true;
        public double m_defaultgain = 0.05;
        public double m_defaultoffset = -3234;
        public bool m_spectraldataenable = false;

        public int m_dataexportset = 1;
        public string m_DeviceID;
        public string m_jsonposturl;

        public string m_MQTTUrl;
        public string m_MQTTtopic;
        public string m_MQTTuser;
        public string m_MQTTpassw;
        public string m_MQTTclientId = Guid.NewGuid().ToString();

        public class NumericValResult
        {
            public string Timestamp;
            public string PhysioID;
            public string Value;
            public string DeviceID;
        }

        public class WaveValResult
        {
            public string Timestamp;
            public string Relativetimestamp;
            public string PhysioID;
            public string Value;
            public double Relativetimecounter;
        }

        //Create a singleton serialport subclass
        private static volatile BSerialPort BPort = null;

        public static BSerialPort getInstance
        {

            get
            {
                if (BPort == null)
                {
                    lock (typeof(BSerialPort))
                        if (BPort == null)
                        {
                            BPort = new BSerialPort();
                        }

                }
                return BPort;
            }

        }

        public BSerialPort()
        {
            BPort = this;

            BPortBufSize = 4096;
            BPort_rxbuf = new byte[BPortBufSize];

            if (OSIsUnix())
                BPort.PortName = "/dev/ttyUSB0"; //default Unix port
            else BPort.PortName = "COM1"; //default Windows port

            BPort.BaudRate = 57600;
            BPort.Parity = Parity.None;
            BPort.DataBits = 8;
            BPort.StopBits = StopBits.One;

            BPort.Handshake = Handshake.None;
            //BPort.RtsEnable = true;
            //BPort.DtrEnable = true;

            // Set the read/write timeouts
            BPort.ReadTimeout = 600000;
            BPort.WriteTimeout = 600000;

            //ASCII Encoding in C# is only 7bit so
            BPort.Encoding = Encoding.GetEncoding("ISO-8859-1");
        }

        public void DebugLine(string msg)
        {
            Debug.WriteLine(DateTime.Now.ToString("hh:mm:ss.fff") + " - " + msg);
        }

        public void RequestProcessedData()
        {
            BPort.WriteBuffer(DataConstants.poll_request_processed_data);
            DebugLine("Send: Request Processed Data");
        }

        public void RequestProcessedSpectralData()
        {
            BPort.WriteBuffer(DataConstants.poll_request_processed_and_spectral_data);
            DebugLine("Send: Request Processed and Spectral Data");
        }

        public void RequestRawEEGData()
        {
            BPort.WriteBuffer(DataConstants.poll_request_raw_eeg_data);
            DebugLine("Send: Request Raw EEG Data");
        }

        public void WriteBuffer(byte[] txbuf)
        {
            List<byte> temptxbufflist = new List<byte>();

            int framelen = txbuf.Length;
            if (framelen != 0)
            {
                //byte[] txbuf2 = new byte[framelen-2];
                //Array.Copy(txbuf, 2, txbuf2, 0, framelen - 2);
                temptxbufflist.AddRange(txbuf);

                byte[] inputbuffer = temptxbufflist.ToArray();

                Crc crccheck = new Crc();
                ushort checksumcomputed = crccheck.ComputeChecksum(inputbuffer);

                byte[] checksumarray = BitConverter.GetBytes(checksumcomputed);

                temptxbufflist.AddRange(checksumarray);
                temptxbufflist.InsertRange(0, DataConstants.spi_id);

                byte[] finaltxbuff = temptxbufflist.ToArray();

                try
                {
                    BPort.Write(finaltxbuff, 0, finaltxbuff.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error opening/writing to serial port :: " + ex.Message, "Error!");
                }

            }
        }

        public async Task SendCycledRequests(int nInterval)
        {
            int nmillisecond = nInterval * 1000;
            if (nmillisecond != 0)
            {
                do
                {
                    if (m_spectraldataenable == true) RequestProcessedSpectralData();
                    else RequestProcessedData();
                    await Task.Delay(nmillisecond);

                }
                while (true);
            }
            if (m_spectraldataenable == true) RequestProcessedSpectralData();
            else RequestProcessedData();
        }

        public async Task SendCycledWaveRequests(int nInterval)
        {
            int nmillisecond = nInterval * 1000;
            if (nmillisecond != 0)
            {
                do
                {
                    RequestRawEEGData();
                    await Task.Delay(nmillisecond);

                }
                while (true);
            }
            RequestRawEEGData();
        }

        public void ClearReadBuffer()
        {
            //Clear the buffer
            for (int i = 0; i < BPortBufSize; i++)
            {
                BPort_rxbuf[i] = 0;
            }
        }

        public int ReadBuffer()
        {
            int bytesreadtotal = 0;

            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "BISVRawoutput.raw");

                int lenread = 0;

                do
                {
                    ClearReadBuffer();
                    lenread = BPort.Read(BPort_rxbuf, 0, BPortBufSize);

                    if (lenread != 0)
                    {
                        byte[] copyarray = new byte[lenread];

                        Buffer.BlockCopy(BPort_rxbuf, 0, copyarray, 0, lenread);

                        m_BufferByteList.AddRange(copyarray);
                        byte[] BufferArray = m_BufferByteList.ToArray();
                        //Once buffer array has been saved clear the list to add last segment if needed
                        m_BufferByteList.Clear();

                        List<byte[]> segments = new List<byte[]>();
                        byte[] lastsegment = Array.Empty<byte>();

                        if (BufferArray.Length > 0)
                            lastsegment = SplitArrayByDelimiter(BufferArray, segments);
                        if (lastsegment != null)
                            m_BufferByteList.AddRange(lastsegment);

                        if (segments.Count > 0)
                        {
                            foreach (byte[] segment in segments)
                            {
                                m_ResponseByteList.AddRange(segment);
                                ProcessCompleteFrame();
                            }
                        }

                        ByteArrayToFile(path, copyarray, copyarray.GetLength(0));
                        bytesreadtotal += lenread;

                    }

                }
                while (BPort.BytesToRead != 0);

                if (BPort.BytesToRead == 0)
                {
                    if (FrameList.Count > 0)
                    {
                        ReadFrameData();
                        FrameList.Clear();
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening/writing to serial port :: " + ex.Message, "Error!");
            }


            return bytesreadtotal;

        }

        public byte[] SplitArrayByDelimiter(byte[] originalArray, List<byte[]> segments)
        {
            try
            {
                byte[] delimiter = DataConstants.spi_id;  // Set the desired delimiter

                ReadOnlySpan<byte> originalSpan = new ReadOnlySpan<byte>(originalArray);

                int startIndex = 0;
                int delimiterIndex;

                while ((delimiterIndex = SearchBytes(originalSpan.ToArray(), delimiter, startIndex)) != -1)
                {
                    int segmentLength = delimiterIndex - startIndex;
                    if (segmentLength > 0)
                    {
                        byte[] segment = new byte[segmentLength];
                        originalSpan.Slice(startIndex, segmentLength).CopyTo(segment);
                        segments.Add(segment);
                    }
                    startIndex = delimiterIndex + delimiter.Length;
                }

                // Add the remaining part after the last delimiter
                byte[] lastSegment = new byte[originalArray.Length - startIndex];
                if (lastSegment.Length > 0)
                    originalSpan.Slice(startIndex, lastSegment.Length).CopyTo(lastSegment);

                //if (BitConverter.ToInt16(lastSegment) != BitConverter.ToInt16(DataConstants.spi_id))
                return lastSegment;
                //else return null;
                // Now 'segments' contains the split frames, and 'lastSegment' contains the remaining bytes
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening/writing to serial port :: " + ex.Message, "Error!");
                return null;
            }
        }

        public static int SearchBytes(byte[] haystack, byte[] needle, int startindex)
        {
            //KMP Search algorithm
            var len = needle.Length;
            var limit = haystack.Length - len;

            for (var i = startindex; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i; // Found the needle at index i in the haystack
            }

            return -1; // Needle not found
        }

        private void ProcessCompleteFrame()
        {
            int framelen = m_ResponseByteList.Count;
            if (framelen >= 2)
            {
                byte[] bArray = m_ResponseByteList.ToArray();

                // Serial data without checksum bytes
                int userdataframelen = framelen - 2;
                byte[] userdataArray = new byte[userdataframelen];
                Array.Copy(bArray, 0, userdataArray, 0, userdataframelen);

                // Calculate checksum
                Crc crccheck = new Crc();
                ushort checksumcomputed = crccheck.ComputeChecksum(userdataArray);

                byte[] bchecksum = new byte[2];
                Array.Copy(bArray, framelen - 2, bchecksum, 0, 2);
                ushort checksum = BitConverter.ToUInt16(bchecksum, 0);

                if (checksumcomputed == checksum)
                {
                    FrameList.Add(userdataArray);
                }
                else
                {
                    Console.WriteLine("Checksum Error");
                }

                m_ResponseByteList.Clear();
            }

        }

        public void ReadFrameData()
        {
            if (FrameList.Count > 0)
            {
                foreach (byte[] fArray in FrameList)
                {
                    ProcessPacket(fArray);
                }
            }
        }

        public void ProcessPacket(byte[] packetbuffer)
        {
            if (packetbuffer.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(packetbuffer);
                BinaryReader binreader = new BinaryReader(memstream);

                uint packetseqid = binreader.ReadUInt16();
                uint odlen = binreader.ReadUInt16();
                uint ldpackettype = binreader.ReadUInt16();

                byte[] datapacket = binreader.ReadBytes(packetbuffer.Length - 6);

                switch (ldpackettype)
                {
                    case DataConstants.L1_DATA_PACKET:
                        ReadDataPacket(datapacket);
                        break;
                    case DataConstants.L1_ACK_PACKET:
                        //ACK
                        break;
                    case DataConstants.L1_NAK_PACKET:
                        //NAK
                        break;
                    default:
                        break;
                }

            }

        }

        public void ReadDataPacket(byte[] datapacketbuffer)
        {
            if (datapacketbuffer.Length != 0)
            {
                m_strTimestamp = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);

                MemoryStream memstream = new MemoryStream(datapacketbuffer);
                BinaryReader binreader = new BinaryReader(memstream);

                uint routingid = binreader.ReadUInt32();
                uint messageid = binreader.ReadUInt32();
                uint seqnum = binreader.ReadUInt16();
                uint messagelen = binreader.ReadUInt16();

                byte[] messagedata = binreader.ReadBytes(datapacketbuffer.Length - 12);

                switch (messageid)
                {
                    case DataConstants.M_PROCESSED_VARS:
                        ReadProcessedEEGData(messagedata);
                        SaveNumericValueListRows("Numerics1");
                        break;
                    case DataConstants.M_PROCESSED_VARS_AND_SPECTRA:
                        ReadProcessedEEGWithSpectralData(messagedata);
                        SaveNumericValueListRows("Numerics2");
                        break;
                    case DataConstants.M_DATA_RAW_EEG:
                        ReadRawEEGDataPacket(messagedata);
                        ExportWaveToCSV();
                        break;
                    default:
                        break;
                }
            }

        }

        public void ReadProcessedEEGWithSpectralData(byte[] procspectralpacket)
        {
            int packetlen = procspectralpacket.Length;
            if (procspectralpacket.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(procspectralpacket);
                BinaryReader binreader = new BinaryReader(memstream);

                //Processed EEG var packets is 120 bytes
                byte[] dsc_information = binreader.ReadBytes(24);
                ReadDSCInfo(dsc_information);

                byte[] impedance_info = binreader.ReadBytes(8);
                byte[] filter = binreader.ReadBytes(4);
                byte[] smoothing = binreader.ReadBytes(4);
                byte[] mask = binreader.ReadBytes(8);

                int trendinfolen = 24;
                bool extrainfo = false;
                if (packetlen == 400)
                {
                    trendinfolen = 36; //extra info with spectral type packet is 400 bytes
                    extrainfo = true;
                }

                byte[] trend_variables1 = binreader.ReadBytes(trendinfolen);
                byte[] trend_variables2 = binreader.ReadBytes(trendinfolen);
                byte[] trend_variables3 = binreader.ReadBytes(trendinfolen);

                ReadEEGVariables(trend_variables1, extrainfo);

                //Spectral EEG data is 244 bytes
                byte[] spectral_info = binreader.ReadBytes(244);

                ReadSpectralData(spectral_info);
            }
        }

        public void ReadSpectralData(byte[] spectraldatabuffer)
        {
            // SIZE_OF_HOST_POWER_SPECTRUM is 60
            // 60 values for 0.5 hz - 30.0 hz, Number of spectra per message is 2
            // SIZE_M_SPECTRA ((SIZE_OF_HOST_POWER_SPECTRUM + 1)*4)

            if (spectraldatabuffer.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(spectraldatabuffer);
                BinaryReader binreader = new BinaryReader(memstream);

                int nchannels = binreader.ReadUInt16();
                int nspectsize = binreader.ReadUInt16();

                int rawspectdatalen = (spectraldatabuffer.Length - 4);
                byte[] rawspectdatach1 = binreader.ReadBytes(rawspectdatalen / 2); //120 bytes
                byte[] rawspectdatach2 = binreader.ReadBytes(rawspectdatalen / 2); //120 bytes
                
                ReadChannelSpectralData(rawspectdatach1, "Ch1");
                ReadChannelSpectralData(rawspectdatach1, "Ch2");
      
            }
        }

        public void ReadChannelSpectralData(byte[] channelspectralbuffer, string channel)
        {
            MemoryStream memstream = new MemoryStream(channelspectralbuffer);
            BinaryReader binreader = new BinaryReader(memstream);

            for (int i = 0; i < (DataConstants.SIZE_OF_HOST_POWER_SPECTRUM); i++) //60 values
            {
                byte[] bvalue = binreader.ReadBytes(2);

                short spectvalue = BitConverter.ToInt16(bvalue, 0); //unit is db*100

                double spectscaledvalue = spectvalue * 0.01; 
                //double spectscaledvalue2 = (double) 10*(Math.Log10(spectscaledvalue));

                string ChPowerx = string.Format(channel + "Power{0}Hz", (i + 1)*0.5);
                string strChPowerValue = ValidateAddData(ChPowerx, spectscaledvalue, 1, false, "{0:0.00}");
            }
        }

        public void ReadProcessedEEGData(byte[] procdatapacketbuffer)
        {
            int packetlen = procdatapacketbuffer.Length;
            if (procdatapacketbuffer.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(procdatapacketbuffer);
                BinaryReader binreader = new BinaryReader(memstream);

                //Processed EEG var packets is 120 bytes
                byte[] dsc_information = binreader.ReadBytes(24);
                ReadDSCInfo(dsc_information);

                byte[] impedance_info = binreader.ReadBytes(8);
                byte[] filter = binreader.ReadBytes(4);
                byte[] smoothing = binreader.ReadBytes(4);
                byte[] mask = binreader.ReadBytes(8);

                int trendinfolen = 24;
                bool extrainfo = false;
                if (packetlen == 156)
                {
                    trendinfolen = 36; //extra info type packet is 156 bytes
                    extrainfo = true;
                }

                byte[] trend_variables1 = binreader.ReadBytes(trendinfolen);
                byte[] trend_variables2 = binreader.ReadBytes(trendinfolen);
                byte[] trend_variables3 = binreader.ReadBytes(trendinfolen);

                ReadEEGVariables(trend_variables1, extrainfo);

            }

        }

        public void ReadDSCInfo(byte[] dscinfopacket)
        {
            if (dscinfopacket.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(dscinfopacket);
                BinaryReader binreader = new BinaryReader(memstream);

                byte[] idinfo = binreader.ReadBytes(8);

                m_Dsc_Info_Struct.dsc_gain_num = binreader.ReadInt32();
                m_Dsc_Info_Struct.dsc_gain_divisor = binreader.ReadInt32();
                m_Dsc_Info_Struct.dsc_offset_num = binreader.ReadInt32();
                m_Dsc_Info_Struct.dsc_offset_divisor = binreader.ReadInt32();

            }

        }

        public double ScaleADCValue(short Waveval)
        {
            dsc_info_struct dscscaledata = m_Dsc_Info_Struct;
            if (!double.IsNaN(Waveval))
            {
                double gain = m_defaultgain;
                double offset = m_defaultoffset;
                double value = 0;

                //Get value from 16 bit ADC values using offset and gain
                if (dscscaledata != null && dscscaledata.dsc_gain_divisor != 0 && dscscaledata.dsc_offset_divisor != 0)
                {
                    gain = (double)dscscaledata.dsc_gain_num / dscscaledata.dsc_gain_divisor;
                    offset = (double)dscscaledata.dsc_offset_num / dscscaledata.dsc_offset_divisor;
                }

                value = (double)gain * (Waveval - offset);
                value = Math.Round(value, 2);

                return value;
            }
            else return Waveval;
        }

        public void ReadEEGVariables(byte[] eegvariablespacket, bool extrainfo)
        {
            int packetlen = eegvariablespacket.Length;

            MemoryStream memstream = new MemoryStream(eegvariablespacket);
            BinaryReader binreader = new BinaryReader(memstream);

            short sBSR = binreader.ReadInt16();
            short sSEF95 = binreader.ReadInt16();
            short sBISBITS = binreader.ReadInt16();
            short sBIS = binreader.ReadInt16();
            short sBIS_ALT = binreader.ReadInt16();
            short sBIS_ALT2 = binreader.ReadInt16();
            short sTPOWER = binreader.ReadInt16();
            short sEMG_LOW = binreader.ReadInt16();
            int iSQI = binreader.ReadInt32();
            int iS_ARTF = binreader.ReadInt32();

            string BSR = ValidateAddData("BSR", sBSR, 0.1, false, "{0:0.0}");
            string SEF95 = ValidateAddData("SEF95", sSEF95, 0.01, false, "{0:0.00}");
            string BIS = ValidateAddData("BIS", sBIS, 0.1, true);
            //string BIS_ALT = ValidateAddData("BIS_ALT", sBIS_ALT, 0.1, false);
            //string BIS_ALT2 = ValidateAddData("BIS_ALT2", sBIS_ALT2, 0.1, false);
            string TPOWER = ValidateAddData("TPOWER", sTPOWER, 0.01, false, "{0:0.00}");
            string EMG_LOW = ValidateAddData("EMG_LOW", sEMG_LOW, 0.01, false, "{0:0.00}");
            string SQI = ValidateAddData("SQI", iSQI, 0.1, false, "{0:0.0}");

            //if (packetlen == 36)
            if(extrainfo == true)
            {
                short burst_per_min = binreader.ReadInt16();
                short rfu1 = binreader.ReadInt16();
                short rfu2 = binreader.ReadInt16();
                short rfu3 = binreader.ReadInt16();
                short rfu4 = binreader.ReadInt16();
                short rfu5 = binreader.ReadInt16();

                string BURSTC = ValidateAddData("BURSTC", burst_per_min, 1, true);
            }

            Console.WriteLine(m_strTimestamp);
            //Console.WriteLine("BSR {0:d}% SEF95 {1:d}Hz BIS {2:d} BIS_ALT{3:d} BIS_ALT2 {4:d}", BSR, SEF95, BIS, BIS_ALT, BIS_ALT2);
            Console.WriteLine("BSR {0:d}% SEF95 {1:d}Hz BIS {2:d} ", BSR, SEF95, BIS);
            Console.WriteLine("TPOWER {0:d}dB EMG_LOW {1:d}dB SQI {2:d}% ", TPOWER, EMG_LOW, SQI);
            Console.WriteLine();

        }

        public string ValidateAddData(string physio_id, object value, double decimalshift, bool rounddata)
        {
            int val = Convert.ToInt32(value);
            double dval = Convert.ToDouble(value, CultureInfo.InvariantCulture) * decimalshift;
            if (rounddata) dval = Math.Round(dval);

            string valuestr = dval.ToString(CultureInfo.InvariantCulture);

            if (val <= DataConstants.DATA_INVALID_LIMIT)
            {
                valuestr = "-";
            }

            NumericValResult NumVal = new NumericValResult();

            NumVal.Timestamp = m_strTimestamp;
            NumVal.PhysioID = physio_id;
            NumVal.Value = valuestr;
            NumVal.DeviceID = m_DeviceID;

            m_NumericValList.Add(NumVal);
            m_NumValHeaders.Add(NumVal.PhysioID);

            return valuestr;
        }

        public string ValidateAddData(string physio_id, object value, double decimalshift, bool rounddata, string decimalformat)
        {
            int val = Convert.ToInt32(value);
            double dval = Convert.ToDouble(value, CultureInfo.InvariantCulture) * decimalshift;
            if (rounddata) dval = Math.Round(dval);

            string valuestr = String.Format(CultureInfo.InvariantCulture, decimalformat, dval);

            if (val <= DataConstants.DATA_INVALID_LIMIT)
            {
                valuestr = "-";
            }

            NumericValResult NumVal = new NumericValResult();

            NumVal.Timestamp = m_strTimestamp;
            NumVal.PhysioID = physio_id;
            NumVal.Value = valuestr;
            NumVal.DeviceID = m_DeviceID;

            m_NumericValList.Add(NumVal);
            m_NumValHeaders.Add(NumVal.PhysioID);

            return valuestr;
        }

        public void ReadRawEEGDataPacket(byte[] rawdatapacketbuffer)
        {
            if (rawdatapacketbuffer.Length != 0)
            {
                MemoryStream memstream = new MemoryStream(rawdatapacketbuffer);
                BinaryReader binreader = new BinaryReader(memstream);

                int nchannels = binreader.ReadInt16();
                int nspeed = binreader.ReadInt16();

                int raweegdatalen = (rawdatapacketbuffer.Length - 4);
                byte[] raweegdata = binreader.ReadBytes(raweegdatalen);
                byte[] eegch1 = new byte[2];
                byte[] eegch2 = new byte[2];

                for (int i = 0; i < raweegdatalen; i = i + 4)
                {
                    Array.Copy(raweegdata, i, eegch1, 0, 2);
                    Array.Copy(raweegdata, i + 2, eegch2, 0, 2);

                    //short eegch1data = TwosComplementToInt16(eegch1);
                    //short eegch2data = TwosComplementToInt16(eegch2);

                    short eegch1data = BitConverter.ToInt16(eegch1, 0);
                    short eegch2data = BitConverter.ToInt16(eegch2, 0);

                    WaveValResult WaveVal1 = new WaveValResult();

                    WaveVal1.Relativetimecounter = m_RealtiveTimeCounter;
                    WaveVal1.Relativetimestamp = m_RealtiveTimeCounter.ToString(CultureInfo.InvariantCulture);

                    WaveVal1.Timestamp = m_strTimestamp;
                    WaveVal1.PhysioID = "EEG1";

                    if (m_calibratewavevalues == true)
                    {
                        //Scale and Range Value in ADC
                        double eegch1val = ScaleADCValue(eegch1data);
                        WaveVal1.Value = eegch1val.ToString(CultureInfo.InvariantCulture);
                    }
                    else WaveVal1.Value = eegch1data.ToString(CultureInfo.InvariantCulture);

                    m_WaveValResultList.Add(WaveVal1);

                    WaveValResult WaveVal2 = new WaveValResult();

                    WaveVal2.Relativetimecounter = m_RealtiveTimeCounter;
                    WaveVal2.Relativetimestamp = m_RealtiveTimeCounter.ToString(CultureInfo.InvariantCulture);

                    WaveVal2.Timestamp = m_strTimestamp;
                    WaveVal2.PhysioID = "EEG2";

                    if (m_calibratewavevalues == true)
                    {
                        //Scale and Range Value in ADC
                        double eegch2val = ScaleADCValue(eegch2data);
                        WaveVal2.Value = eegch2val.ToString(CultureInfo.InvariantCulture);

                    }
                    else WaveVal2.Value = eegch2data.ToString(CultureInfo.InvariantCulture);

                    m_WaveValResultList.Add(WaveVal2);

                    //nspeed eeg packets are read every sec
                    m_RealtiveTimeCounter = (m_RealtiveTimeCounter + (1 / (double)nspeed)); //sec
                    m_RealtiveTimeCounter = Math.Round(m_RealtiveTimeCounter, 3);
                }


            }

        }

        public void ExportWaveToCSV()
        {
            int wavevallistcount = m_WaveValResultList.Count;

            if (wavevallistcount != 0)
            {
                foreach (WaveValResult WavValResult in m_WaveValResultList)
                {
                    if (WavValResult.PhysioID == "EEG1")
                    {
                        m_strbuildwavevalues.Append(WavValResult.Timestamp);
                        m_strbuildwavevalues.Append(',');
                        m_strbuildwavevalues.Append(WavValResult.Relativetimestamp);
                        m_strbuildwavevalues.Append(',');
                        m_strbuildwavevalues.Append(WavValResult.Value);
                        m_strbuildwavevalues.Append(',');
                        m_strbuildwavevalues.AppendLine();

                    }

                    if (WavValResult.PhysioID == "EEG2")
                    {
                        m_strbuildwavevalues2.Append(WavValResult.Timestamp);
                        m_strbuildwavevalues2.Append(',');
                        m_strbuildwavevalues2.Append(WavValResult.Relativetimestamp);
                        m_strbuildwavevalues2.Append(',');
                        m_strbuildwavevalues2.Append(WavValResult.Value);
                        m_strbuildwavevalues2.Append(',');
                        m_strbuildwavevalues2.AppendLine();

                    }


                }

                string pathcsv1 = Path.Combine(Directory.GetCurrentDirectory(), "EEG1WaveExport.csv");
                string pathcsv2 = Path.Combine(Directory.GetCurrentDirectory(), "EEG2WaveExport.csv");

                ExportNumValListToCSVFile(pathcsv1, m_strbuildwavevalues);
                ExportNumValListToCSVFile(pathcsv2, m_strbuildwavevalues2);

                m_strbuildwavevalues.Clear();
                m_strbuildwavevalues2.Clear();
                m_WaveValResultList.Clear();
            }

        }

        public short TwosComplementToInt16(byte[] bArray)
        {
            //BIS monitor is LittleEndian
            short result = BitConverter.ToInt16(new byte[] { bArray[0], bArray[1] }); //lsb, msb

            //int resultlsb = (sbyte) bArray[1];
            //byte resultmsb = bArray[0];
            //short result2 = (short) ((resultlsb << 8) | resultmsb);

            return result;
        }

        public static ushort correctendianshortus(byte[] bArray)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bArray);

            ushort result = BitConverter.ToUInt16(bArray, 0);
            return result;
        }

        public static uint correctendianuint(byte[] bArray)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bArray);

            uint result = BitConverter.ToUInt32(bArray, 0);
            return result;
        }
        public void StopTransfer()
        {
            WriteBuffer(DataConstants.poll_stop_processed_data);
            DebugLine("Send: Stop Processed Data");
            WriteBuffer(DataConstants.poll_stop_raw_eeg_data);
            DebugLine("Send: Stop Raw EEG Data");
            this.Dispose();
        }

        bool WriteHeadersForDatatype(string datatype)
        {
            bool writeheader = true;
            switch (datatype)
            {
                case "Numerics1":
                    if (m_transmissionstart)
                    {
                        m_transmissionstart = false;

                    }
                    else writeheader = false;
                    break;
                case "Numerics2":
                    if (m_transmissionstart2)
                    {
                        m_transmissionstart2 = false;

                    }
                    else writeheader = false;
                    break;

            }

            return writeheader;
        }

        public void WriteNumericHeadersList(string datatype)
        {
            if (m_NumericValList.Count != 0 && (WriteHeadersForDatatype(datatype)))
            {
                string filename = String.Format("{0}BISDataExport.csv", datatype);

                string pathcsv = Path.Combine(Directory.GetCurrentDirectory(), filename);

                m_strbuildheaders.Append("Time");
                m_strbuildheaders.Append(',');


                foreach (NumericValResult NumValResult in m_NumericValList)
                {
                    m_strbuildheaders.Append(NumValResult.PhysioID);
                    m_strbuildheaders.Append(',');

                }

                m_strbuildheaders.Remove(m_strbuildheaders.Length - 1, 1);
                m_strbuildheaders.Replace(",,", ",");
                m_strbuildheaders.AppendLine();
                ExportNumValListToCSVFile(pathcsv, m_strbuildheaders);

                m_strbuildheaders.Clear();
                m_NumValHeaders.RemoveRange(0, m_NumValHeaders.Count);

            }
        }

        public void SaveNumericValueListRows(string datatype)
        {
            if (m_dataexportset == 2) ExportNumValListToJSON(datatype);
            if (m_dataexportset == 3) ExportNumValListToMQTT(datatype);
            if (m_dataexportset == 4) ExportNumValListToJSONFile(datatype);
            if (m_dataexportset != 3 && m_dataexportset != 4)
            {
                if (m_NumericValList.Count != 0)
                {
                    WriteNumericHeadersList(datatype);
                    string filename = String.Format("{0}BISDataExport.csv", datatype);

                    string pathcsv = Path.Combine(Directory.GetCurrentDirectory(), filename);

                    m_strbuildvalues.Append(m_NumericValList.ElementAt(0).Timestamp);
                    m_strbuildvalues.Append(',');


                    foreach (NumericValResult NumValResult in m_NumericValList)
                    {
                        m_strbuildvalues.Append(NumValResult.Value);
                        m_strbuildvalues.Append(',');

                    }

                    m_strbuildvalues.Remove(m_strbuildvalues.Length - 1, 1);
                    m_strbuildvalues.Replace(",,", ",");
                    m_strbuildvalues.AppendLine();

                    ExportNumValListToCSVFile(pathcsv, m_strbuildvalues);
                    m_strbuildvalues.Clear();
                    m_NumericValList.RemoveRange(0, m_NumericValList.Count);
                }
            }

        }

        public void ExportNumValListToCSVFile(string _FileName, StringBuilder strbuildNumVal)
        {
            try
            {
                // Open file for reading. 
                using (StreamWriter wrStream = new StreamWriter(_FileName, true, Encoding.UTF8))
                {
                    wrStream.Write(strbuildNumVal);
                    strbuildNumVal.Clear();

                    // close file stream. 
                    wrStream.Close();
                }

            }

            catch (Exception _Exception)
            {
                // Error. 
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
            }

        }
        public bool ByteArrayToFile(string _FileName, byte[] _ByteArray, int nWriteLength)
        {
            try
            {
                // Open file for reading. 
                using (FileStream _FileStream = new FileStream(_FileName, FileMode.Append, FileAccess.Write))
                {
                    // Writes a block of bytes to this stream using data from a byte array
                    _FileStream.Write(_ByteArray, 0, nWriteLength);

                    // close file stream. 
                    _FileStream.Close();
                }

                return true;
            }

            catch (Exception _Exception)
            {
                // Error. 
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
            }
            // error occured, return false. 
            return false;
        }

        public void ExportNumValListToJSON(string datatype)
        {
            string serializedJSON = JsonSerializer.Serialize(m_NumericValList, new JsonSerializerOptions { IncludeFields = true });

            try
            {
                // Open file for reading. 
                //using (StreamWriter wrStream = new StreamWriter(pathjson, true, Encoding.UTF8))
                //{
                // wrStream.Write(serializedJSON);
                //  wrStream.Close();
                //}

                Task.Run(() => PostJSONDataToServer(serializedJSON));

            }

            catch (Exception _Exception)
            {
                // Error. 
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
            }
        }

        public void ExportNumValListToJSONFile(string datatype)
        {
            string serializedJSON = JsonSerializer.Serialize(m_NumericValList, new JsonSerializerOptions { IncludeFields = true });

            m_NumericValList.RemoveRange(0, m_NumericValList.Count);

            string filename = String.Format("DataExportVSC.json");

            string pathjson = Path.Combine(Directory.GetCurrentDirectory(), filename);

            try
            {
                // Open file for reading. 
                using (StreamWriter wrStream = new StreamWriter(pathjson, true, Encoding.UTF8))
                {
                    wrStream.Write(serializedJSON);

                    wrStream.Close();
                }
            }

            catch (Exception _Exception)
            {
                // Error. 
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
            }
        }

        public async Task PostJSONDataToServer(string postData)
        {
            using (HttpClient client = new HttpClient())
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                var data = new StringContent(postData, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(m_jsonposturl, data);
                response.EnsureSuccessStatusCode();

                string result = await response.Content.ReadAsStringAsync();

                Console.WriteLine(result);
            }
        }

        public void ExportNumValListToMQTT(string datatype)
        {
            string serializedJSON = JsonSerializer.Serialize(m_NumericValList, new JsonSerializerOptions { IncludeFields = true });

            m_NumericValList.RemoveRange(0, m_NumericValList.Count);

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var mqttClient = new MqttFactory().CreateMqttClient();
            var logger = new MqttFactory().DefaultLogger;
            var managedClient = new ManagedMqttClient(mqttClient, logger);

            var topic = m_MQTTtopic + string.Format("/{0}", datatype);

            try
            {
                var task = Task.Run(async () =>
                {
                    var connected = GetConnectedTask(managedClient);
                    await ConnectMQTTAsync(managedClient, token, m_MQTTUrl, m_MQTTclientId, m_MQTTuser, m_MQTTpassw);
                    await connected;

                    //await PublishMQTTAsync(managedClient, token, topic, serializedJSON);
                    //await managedClient.StopAsync();
                });

                task.ContinueWith(antecedent =>
                {
                    if (antecedent.Status == TaskStatus.RanToCompletion)
                    {
                        Task.Run(async () =>
                        {
                            await PublishMQTTAsync(managedClient, token, topic, serializedJSON);
                            await managedClient.StopAsync();
                        });
                    }
                });

            }

            catch (Exception _Exception)
            {
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
            }

        }

        Task GetConnectedTask(ManagedMqttClient managedClient)
        {
            TaskCompletionSource<bool> connected = new TaskCompletionSource<bool>();
            managedClient.ConnectedAsync += (MqttClientConnectedEventArgs arg) =>
            {

                connected.SetResult(true);
                //Console.WriteLine("MQTT Client connected");
                return Task.CompletedTask;
            };

            return connected.Task;

        }

        public static async Task ConnectMQTTAsync(ManagedMqttClient mqttClient, CancellationToken token, string mqtturl, string clientId, string mqttuser, string mqttpassw)
        {
            bool mqttSecure = true;

            var messageBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithCredentials(mqttuser, mqttpassw)
            .WithCleanSession()
            .WithWebSocketServer((MqttClientWebSocketOptionsBuilder b) =>
            {
                b.WithUri(mqtturl);
            });

            var tlsOptions = new MqttClientTlsOptionsBuilder()
               .WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12)
               .Build();

            var options = mqttSecure
            ? messageBuilder
            .WithTlsOptions(tlsOptions)
                .Build()
            : messageBuilder
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
              .WithAutoReconnectDelay(TimeSpan.FromSeconds(1))
              .WithClientOptions(options)
              .Build();

            await mqttClient.StartAsync(managedOptions);

        }

        public static async Task PublishMQTTAsync(ManagedMqttClient mqttClient, CancellationToken token, string topic, string payload, bool retainFlag = true, int qos = 1)
        {
            await mqttClient.EnqueueAsync(topic, payload, (MQTTnet.Protocol.MqttQualityOfServiceLevel)qos, retainFlag);
            //Console.WriteLine("The managed MQTT client is connected, publishing data.");

            // Wait until the queue is fully processed.
            SpinWait.SpinUntil(() => mqttClient.PendingApplicationMessagesCount == 0, 5000);
            //Console.WriteLine($"Pending messages = {mqttClient.PendingApplicationMessagesCount}");

        }


        public bool OSIsUnix()
        {
            int p = (int)Environment.OSVersion.Platform;
            if ((p == 4) || (p == 6) || (p == 128)) return true;
            else return false;

        }

    }
}

