# AcaiaLoggerWin10
Logger for Acaia coffee scale, build in C# for Win10 platform. This is an attempt to replicated Acaia' Brewmaster IOS/Android app features.

Was inspired by h1kari' project https://github.com/h1kari/AcaiaScale. But it did not work with Acaia Pearl scale firmware 2.0, as the protocol has changed. To capture the new protocol I collected the HCI Bluetooth logs with an Android tablet running Android 5.0, then analysed the logs with Wireshark. A sample HCI log (which can be used as input to Wireshark) and the workout of the Wireshark output are in the Sample_Android_log folder. A very useful guide is here: https://reverse-engineering-ble-devices.readthedocs.io/en/latest/protocol_reveng/00_protocol_reveng.html 

The reason for using Win10 is because from the Creators update it offers native support for Bluetooth Low Energy devices. I used this sample code to get started: https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/BluetoothLE 
