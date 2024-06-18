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
using System.Reflection;
using System.Runtime.InteropServices;

namespace VSCaptureBISV
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 120, CharSet = CharSet.Ansi)]
    public class processed_vars_msg
    {
        public dsc_info_struct proc_dsc_info = new dsc_info_struct();
        public impedance_info_struct impedance_info1 = new impedance_info_struct();
        public impedance_info_struct impedance_info2 = new impedance_info_struct();
        public uint host_filt_setting;
        public uint host_smoothing_setting;
        public uint host_spectral_art_mask;
        public uint host_bispectral_art_mask;
        public be_trend_variables_info trend_variables1 = new be_trend_variables_info(); //channel 1
        public be_trend_variables_info trend_variables2 = new be_trend_variables_info(); //channel 2
        public be_trend_variables_info trend_variables3 = new be_trend_variables_info(); //channel 1 2
    
    }
   
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 156, CharSet = CharSet.Ansi)]
    public class processed_vars_with_extra_vars_msg
    {
        public dsc_info_struct proc_dsc_info = new dsc_info_struct();
        public impedance_info_struct impedance_info1 = new impedance_info_struct();
        public impedance_info_struct impedance_info2 = new impedance_info_struct();
        public uint host_filt_setting;
        public uint host_smoothing_setting;
        public uint host_spectral_art_mask;
        public uint host_bispectral_art_mask;
        public be_trend_variables_extra_info trend_variables_extra1 = new be_trend_variables_extra_info(); //channel 1
        public be_trend_variables_extra_info trend_variables_extra2 = new be_trend_variables_extra_info(); //channel 2
        public be_trend_variables_extra_info trend_variables_extra3 = new be_trend_variables_extra_info(); //channel 1 2

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24, CharSet = CharSet.Ansi)]
    public class dsc_info_struct
    {
        public byte dsc_id;
        public byte dsc_id_legal;
        public byte pic_id;
        public byte pic_id_legal;
        public ushort dsc_numofchan;
        public ushort quick_test_result;
        public int dsc_gain_num;
        public int dsc_gain_divisor;
        public int dsc_offset_num;
        public int dsc_offset_divisor;

    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4, CharSet = CharSet.Ansi)]
    public class impedance_info_struct
    {
        public ushort impedance_value;
        public ushort imped_test_result;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24, CharSet = CharSet.Ansi)]
    public class be_trend_variables_info
    {
        public short burst_suppress_ratio; /* index variable giving percent of suppressed seconds in last 63 sec.for selected channel. range from 0 - 1000 in .1% steps */
        public short spectral_edge_95; /* in HZ ranged from 0-30.0 Hz in units of 0.01 Hz */
        public short bis_bits; /* BIS field debug data */
        public short bispectral_index; /* Ranges from 0 - 100 */
        public short bispectral_alternate_index; /* same as above */
        public short bispectral_alternate2_index; /* same as above */
        public short total_power; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
        public short emg_low; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
        public int bis_signal_quality; /* index variable giving the signal quality of the bisIndex which is combined with BSR 0 - 1000 in .1% steps*/
        public int second_artifact;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36, CharSet = CharSet.Ansi)]
    public class be_trend_variables_extra_info
    {
        public short burst_suppress_ratio; /* index variable giving percent of suppressed seconds in last 63 sec.for selected channel. range from 0 - 1000 in .1% steps */
        public short spectral_edge_95; /* in HZ ranged from 0-30.0 Hz in units of 0.01 Hz */
        public short bis_bits; /* BIS field debug data */
        public short bispectral_index; /* Ranges from 0 - 100 */
        public short bispectral_alternate_index; /* same as above */
        public short bispectral_alternate2_index; /* same as above */
        public short total_power; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
        public short emg_low; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
        public int bis_signal_quality; /* index variable giving the signal quality of the bisIndex which is combined with BSR 0 - 1000 in .1% steps*/
        public int second_artifact;
        public short burst_per_min;
        public short rfu1;
        public short rfu2;
        public short rfu3;
        public short rfu4;
        public short rfu5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 244, CharSet = CharSet.Ansi)]
    public class spectra_msg
    {
        public ushort spect_numofchan;
        public ushort spect_size;
        //public short power_spectrum[SPECTRA_NUMOFCHAN][SIZE_OF_HOST_POWER_SPECTRUM];
        public short[] power_spectrum1 = new short[60];
        public short[] power_spectrum2 = new short[60];
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 532, CharSet = CharSet.Ansi)]
    public class processed_vars_and_spectra_msg_4b
    {
        public processed_vars_msg_4b processed_vars = new processed_vars_msg_4b();        public spectra_msg spectra = new spectra_msg();    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 288, CharSet = CharSet.Ansi)]
    public class processed_vars_msg_4b
    {
        public dsc_bilateral_info_struct proc_dsc_info = new dsc_bilateral_info_struct();
        public impedance_info_struct impedance_info1 = new impedance_info_struct();
        public impedance_info_struct impedance_info2 = new impedance_info_struct();
        public impedance_info_struct impedance_info3 = new impedance_info_struct();
        public impedance_info_struct impedance_info4 = new impedance_info_struct();
        public uint host_filt_setting;
        public uint host_smoothing_setting;
        public uint host_spectral_art_mask;
        public uint host_bispectral_art_mask;
        public be_bilateral_trend_variables_info trend_variables1 = new be_bilateral_trend_variables_info(); //channel 1
        public be_bilateral_trend_variables_info trend_variables2 = new be_bilateral_trend_variables_info(); //channel 2
        public be_bilateral_trend_variables_info trend_variables3 = new be_bilateral_trend_variables_info(); //channel 3
        public be_bilateral_trend_variables_info trend_variables4 = new be_bilateral_trend_variables_info(); //channel 4
        public short sqi_left_index;
        public short sqi_right_index;
        public short bis_left_index;
        public short bis_right_index;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 72, CharSet = CharSet.Ansi)]
    public class dsc_bilateral_info_struct
    {
        public byte dsc_id;
        public byte dsc_id_legal;
        public byte pic_id;
        public byte pic_id_legal;
        public ushort dsc_numofchan;
        public ushort quick_test_result;
        public short dsc_update_status;
        public short pad1;
        public int dsc_gain_num;
        public int dsc_gain_divisor;
        public int dsc_offset_num;
        public int dsc_offset_divisor;
        public short sensor_status;
        public bilateral_sensor_description_struct sensor_desc = new bilateral_sensor_description_struct();
        public short pad2;
        public int lot_code;
        public short shelf_life;
        public short serial_number;
        public int usage_count;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 44, CharSet = CharSet.Ansi)]
    public class be_bilateral_trend_variables_info
    {
       public short burst_suppress_ratio; /* index variable giving percent of suppressed seconds in last 63 sec.for selected channel. range from 0 - 1000 in .1% steps */
       public short spectral_edge_95; /* in HZ ranged from 0-30.0 Hz in units of 0.01 Hz */
       public short spectral_edge_50; /* in HZ ranged from 0-30.0 Hz in units of 0.01 Hz */
       public short bis_bits; /* BIS field debug data */
       public short bispectral_index; /* Ranges from 0 - 100 */
       public short bispectral_alternate_index; /* same as above */
       public short bispectral_alternate2_index; /* same as above */
       public short total_power; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
       public short emg_low; /* in dB with respect to .01 uv rms. ranged from 0 to 100 dB in 0.01 units */
       public short pad1;
       public int bis_signal_quality; /* index variable giving the signal quality of the bis Index which is combined with BSR 0 - 1000 in .1% steps */
       public int second_artifact;
       public byte burst_count; /* bursts/minute 0 - 30 */
       public byte reserved_byte_var;
       public short asym_index;
       public short std_bis; /* standard deviation of BIS */ 
       public short std_emg;/* standard deviation of EMG */
       public short reserved_short_var_0;
       public short reserved_short_var_1;
       public short reserved_short_var_2;
       public short pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28, CharSet = CharSet.Ansi)]
    public class bilateral_sensor_description_struct
    {
        public byte[] sensor_name = new byte[12];
        public byte sensor_type;
        public byte sensor_graphic_type;
        public byte sensor_eeg_channels;
        public byte eeg_left_display_index;
        public byte eeg_right_display_index;
        public byte sensor_sqi_channels;
        public byte sqi_left_display_index;
        public byte sqi_right_display_index;
        public byte sensor_emg_channels;
        public byte emg_left_display_index;
        public byte emg_right_display_index;
        public byte pad1;
        public byte[] case_id = new byte[4];
    }
    public static class DataConstants
    {
        public const ushort us_spi_id = 0xBAAB;

        public const uint L1_DATA_PACKET = 0x01;
        public const uint L1_ACK_PACKET = 0x02;
        public const uint L1_NAK_PACKET = 0x03;

        public const uint M_DATA_RAW_EEG = 0x32;
        public const uint M_PROCESSED_VARS = 0x34;
        public const uint M_PROCESSED_VARS_AND_SPECTRA = 0x35;

        public const int DATA_INVALID_LIMIT = (-3277); /* limit for special invalid data values */
        public const int SIZE_OF_HOST_POWER_SPECTRUM = 60; /* 60 values for 0.5 hz - 30.0 hz */
        public const int SPECTRA_NUMOFCHAN = 2; /* Number of spectra per message */

        public static byte[] spi_id =
        {
            0xBA, 0XAB
        };

        public static byte[] poll_request_processed_data = {
            0x00, 0x00, 0x0D, 0x00, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x00
        };

        public static byte[] poll_request_processed_and_spectral_data = {
            0x00, 0x00, 0x0D, 0x00, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x01
        };

        public static byte[] poll_request_raw_eeg_data = {
            0x00, 0x00, 0x0E, 0x00, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x6F, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x80, 0x00
        };

        public static byte[] poll_stop_processed_data = {
            0x00, 0x00, 0x0C, 0x00, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x74, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00
        };

        public static byte[] poll_stop_raw_eeg_data = {
            0x00, 0x00, 0x0C, 0x00, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x70, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00
        };
    }
}

