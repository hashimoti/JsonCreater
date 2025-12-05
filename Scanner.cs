// Scanner.cs

using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using System.Collections.Generic;
using System.Linq;

// 検出されたデバイスの情報を保持するレコード
public record DetectedDevice(string Name, ulong Address, short Rssi); 

public class Scanner
{
    private BluetoothLEAdvertisementWatcher _watcher = new BluetoothLEAdvertisementWatcher();
    private readonly HashSet<ulong> _seenAddresses = new HashSet<ulong>();
    private readonly List<DetectedDevice> _detectedDevices = new List<DetectedDevice>();

    public IReadOnlyList<DetectedDevice> DetectedDevices => _detectedDevices;

    public Scanner()
    {
        _watcher.Received += Watcher_Received;
    }

    public void StartScan()
    {
        Console.WriteLine("--- BLEスキャンを開始しました (10秒間) ---");
        _seenAddresses.Clear();
        _detectedDevices.Clear();
        _watcher.Start();
    }

    public void StopScan()
    {
        _watcher.Stop();
        Console.WriteLine("スキャンを停止しました。");
    }

    private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, 
                                 BluetoothLEAdvertisementReceivedEventArgs args)
    {
        string deviceName = args.Advertisement.LocalName;
        ulong deviceAddress = args.BluetoothAddress;
        
        // 名前が空、または既に見つけたアドレスなら無視
        if (string.IsNullOrEmpty(deviceName) || _seenAddresses.Contains(deviceAddress)) return;

        // 新規デバイスを検出
        _seenAddresses.Add(deviceAddress); 
        
        var newDevice = new DetectedDevice(deviceName, deviceAddress, args.RawSignalStrengthInDBm);
        _detectedDevices.Add(newDevice);

        // コンソールに表示
        Console.WriteLine($"[発見 #{_detectedDevices.Count:D2}] {deviceName} (RSSI: {newDevice.Rssi} dBm)");
        Console.WriteLine($"  アドレス (16進数): {newDevice.Address:X}"); 
    }
    
    // スキャンを指定時間実行するためのヘルパー
    public async Task ScanForDurationAsync(int seconds)
    {
        StartScan();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        StopScan();
    }
}