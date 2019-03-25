using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Linq;

namespace AcaiaLogger
{
    public sealed partial class MainPage : Page
    {
        string ScaleServiceGuid = "00001820-0000-1000-8000-00805f9b34fb"; // Internet Protocol Support Service 0x1820
        string ScaleCharactGuid = "00002a80-0000-1000-8000-00805f9b34fb"; // Age		                       0x2A80

        private void WriteHeartBeat()
        {
            // Heartbeat message needs to be send to scale every 3 sec to continue receiving weight measurements
            byte[] payload = new byte[] { 0xef, 0xdd, 0x00, 0x02, 0x00, 0x02, 0x00 };
            writeToScale(payload);
        }

        private void WriteAppIdentity()
        {
            // send app ID to start getting weight notifications
            byte[] payload = new byte[] { 0xef, 0xdd, 0x0b, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0x31, 0x32, 0x33, 0x34, 0x9a, 0x6d };
            writeToScale(payload);
        }

        private void WriteTare()
        {
            // tare command
            byte[] payload = new byte[] { 0xef, 0xdd, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            writeToScale(payload);
        }

        private async void writeToScale(byte[] payload)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await selectedCharacteristic.WriteValueWithResultAsync(payload.AsBuffer());

                if (result.Status != GattCommunicationStatus.Success)
                    FatalError("Failed to write to scale characteristic");
            }
            catch (Exception ex)
            {
                FatalError("Failed to write to scale characteristic " + ex.Message);
            }
        }

        private bool DecodeWeight(byte[] data, ref double weight_gramm, ref bool is_stable)
        {
            if (data == null)
                return false;

            // try to decode data as weight, example: EF-DD-0C-08-05- 64-00-00-00-01-02-6D-07

            if (data.Length != 13)
                return false;

            byte[] weight_pattern = new byte[] { 0xef, 0xdd, 0x0c, 0x08, 0x05 };

            byte[] candidate = new byte[5];
            Array.Copy(data, 0, candidate, 0, 5);
            if (!candidate.SequenceEqual<byte>(weight_pattern))
                return false;

            byte unit = data[9];
            if (unit != 0x01) // Wight unit (byte #10) is not 1, not sure how to decode,  TODO
                FatalError("Unsupported weight format, TODO");

            try
            {
                bool negative = (data[10] & 0x02) != 0;
                is_stable = (data[10] & 0x01) == 0;

                weight_gramm = (negative ? -1.0 : 1.0) * BitConverter.ToInt32(data, 5) / 10.0;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}