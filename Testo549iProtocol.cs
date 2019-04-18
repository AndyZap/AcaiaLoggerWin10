using System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.UI.Xaml.Controls;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Linq;

namespace AcaiaLogger
{
    public sealed partial class MainPage : Page
    {
        string TestoServiceGuid = "0000fff0-0000-1000-8000-00805f9b34fb";
        string TestoCharactNotifGuid = "0000fff2-0000-1000-8000-00805f9b34fb";
        string TestoCharactWriteGuid = "0000fff1-0000-1000-8000-00805f9b34fb";

        private void WriteCommandsToEnablePressureMeasurements()
        {
            // command 1
            byte[] payload = new byte[] { 0x56, 0x00, 0x03, 0x00, 0x00, 0x00, 0x0c, 0x69, 0x02, 0x3e, 0x81 };
            writeToTesto(payload);

            // command 2
            payload = new byte[] { 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x7b };
            writeToTesto(payload);

            // command 3
            payload = new byte[] { 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x5a };
            writeToTesto(payload);
        }

        private async void writeToTesto(byte[] payload)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await selectedCharacteristicTestoWrite.WriteValueWithResultAsync(payload.AsBuffer());

                if (result.Status != GattCommunicationStatus.Success)
                    FatalError("Failed to write to Testo characteristic");
            }
            catch (Exception ex)
            {
                FatalError("Failed to write to Testo characteristic " + ex.Message);
            }
        }

        private bool DecodePressure(byte[] data, ref double pressure_bar)
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
                FatalError("Unsupported pressure format, TODO");

            try
            {
                bool negative = (data[10] & 0x02) != 0;

                pressure_bar = (negative ? -1.0 : 1.0) * BitConverter.ToInt32(data, 5) / 10.0;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}