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

        private void WriteZeroPressure()
        {
            byte[] payload = new byte[] { 0x05, 0x00, 0x13, 0x00, 0x00, 0x00, 0x04, 0xca, 0x0c, 0x00, 0x00, 0x00, 0x5a, 0x65, 0x72, 0x6f, 0x50, 0x72, 0x65, 0x73 };
            writeToTesto(payload);

            payload = new byte[] { 0x73, 0x75, 0x72, 0x65, 0x01, 0x62, 0xe5 };
            writeToTesto(payload);
        }

        private async void writeToTesto(byte[] payload)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await characteristicTestoWrite.WriteValueWithResultAsync(payload.AsBuffer());

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

            /* example pressure record

            10 80 20 00 00 00 08 95 14 00 00 00 44 69 66 66 65 72 65 6e
            74 69 61 6c 50 72 65 73 73 75 72 65 00 d0 e9 45 00 00 37 37

            we are interested in 4 bytes at offset 32, i.e. 00 d0 e9 45 00 - which are float value in Pa = 1E-5 bar */

            if (data.Length != 40)
                return false;

            byte[] pressure_pattern = new byte[] { 0x10, 0x80, 0x20, 0x00, 0x00 };

            byte[] candidate = new byte[5];
            Array.Copy(data, 0, candidate, 0, 5);
            if (!candidate.SequenceEqual<byte>(pressure_pattern))
                return false;

            try
            {
                pressure_bar = BitConverter.ToSingle(data, 32) * 1E-5;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}