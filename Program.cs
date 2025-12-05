// Program.cs

using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions; // 正規表現のために追加

// DeviceProfile.cs を別途用意するか、このクラス定義を使用してください
public class DeviceProfile
{
    public string Name { get; set; } = string.Empty;
    public string AddressHex { get; set; } = string.Empty;
    public string ServiceUuid { get; set; } = string.Empty;
    public string CharacteristicUuid { get; set; } = string.Empty;
    public string ParserType { get; set; } = string.Empty;
}


public class Program
{
    private const string ProfileFileName = "profiles.json";
    private const int ScanDurationSeconds = 10;
    
    // ★★★ 固定値定義 ★★★
    private const string FixedServiceUuid = "0000180D-0000-1000-8000-00805F9B34FB";
    private const string FixedCharacteristicUuid = "00002A37-0000-1000-8000-00805F9B34FB";
    private const string FixedParserType = "HeartRate";
    // ★★★ ---------------- ★★★


    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- JsonCreater: BLE プロファイルビルダー ---");
        Console.WriteLine($"[固定設定] Service UUID: {FixedServiceUuid}");
        
        var scanner = new Scanner();
        
        // 1. スキャンを実行し、デバイスを検出
        await scanner.ScanForDurationAsync(ScanDurationSeconds); 

        if (!scanner.DetectedDevices.Any())
        {
            Console.WriteLine($"\n{ScanDurationSeconds}秒以内にデバイスが検出されませんでした。");
            return;
        }

        // 2. ユーザーにデバイスを選択させる
        Console.WriteLine("\n--- プロファイル作成対象のデバイスを選択 ---");
        DetectedDevice? selectedDevice = SelectDevice(scanner.DetectedDevices);
        
        if (selectedDevice == null) return;
        
        Console.WriteLine($"\n=> 選択デバイス: {selectedDevice.Name} ({selectedDevice.Address:X})");
        
        // 3. 固定値を使ってプロファイルを作成
        DeviceProfile newProfile = CreateFixedProfile(selectedDevice);
        
        // 4. プロファイルをファイルに保存
        await SaveProfileAsync(newProfile);
    }

    private static DetectedDevice? SelectDevice(IReadOnlyList<DetectedDevice> devices)
    {
        Console.Write($"番号 (1-{devices.Count}) を入力してください: ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= devices.Count)
        {
            return devices[choice - 1];
        }
        Console.WriteLine("無効な選択です。プログラムを終了します。");
        return null;
    }

    private static DeviceProfile CreateFixedProfile(DetectedDevice device)
    {
        string addressHex = device.Address.ToString("X");
        
        Console.WriteLine("\n--- 固定設定を適用します ---");
        
        return new DeviceProfile
        {
            // ★修正: スキャンで見つけた名前をそのまま使用 (サフィックス無し) ★
            Name = device.Name, 
            AddressHex = addressHex,
            ServiceUuid = FixedServiceUuid,
            CharacteristicUuid = FixedCharacteristicUuid,
            ParserType = FixedParserType
        };
    }
    
    // ★新規メソッド: 重複チェックと新しい名前の生成★
    private static string FindNextAvailableName(string baseName, List<DeviceProfile> existingProfiles)
    {
        // 既存のプロファイル名から、同じベース名を持つものを抽出
        var matchingNames = existingProfiles
            .Select(p => p.Name)
            .Where(n => n.StartsWith(baseName))
            .ToList();

        if (matchingNames.Count == 0)
        {
            return baseName; // 重複がなければそのまま
        }

        // 正規表現: ベース名に続くサフィックス（-01, -02, ...）を検索
        // 例: "HW706-0029277-01" -> "-01"
        string pattern = $"^{Regex.Escape(baseName)}-(\\d+)$";
        int maxIndex = 0;

        foreach (var name in matchingNames)
        {
            if (name == baseName)
            {
                // サフィックスなしの重複を見つけた場合、インデックスを1にする
                maxIndex = Math.Max(maxIndex, 1);
                continue;
            }

            var match = Regex.Match(name, pattern);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int index))
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }
        }
        
        // 新しいインデックスは最大インデックス + 1
        int newIndex = maxIndex + 1;

        // 連番を2桁でフォーマットして返す (例: -01, -02, ...)
        return $"{baseName}-{newIndex:D2}";
    }

    private static async Task SaveProfileAsync(DeviceProfile newProfile)
    {
        List<DeviceProfile> profiles = new List<DeviceProfile>();
        
        // 既存ファイルを読み込み
        if (File.Exists(ProfileFileName))
        {
            try
            {
                string jsonString = await File.ReadAllTextAsync(ProfileFileName);
                var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                var existingProfiles = JsonSerializer.Deserialize<List<DeviceProfile>>(jsonString, options);
                if (existingProfiles != null)
                {
                    profiles.AddRange(existingProfiles);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"警告: 既存の{ProfileFileName}を読み込めませんでした。新規作成します。エラー: {ex.Message}");
            }
        }
        
        // ★修正: 名前重複チェックと連番生成★
        string baseName = newProfile.Name;
        newProfile.Name = FindNextAvailableName(baseName, profiles);
        
        // 新しいプロファイルを追加
        profiles.Add(newProfile);
        
        // ファイルに書き戻し (整形して書き込み)
        var writeOptions = new JsonSerializerOptions { WriteIndented = true };
        string outputJson = JsonSerializer.Serialize(profiles, writeOptions);
        
        await File.WriteAllTextAsync(ProfileFileName, outputJson);
        
        Console.WriteLine($"\n★★★ 成功! {ProfileFileName} に新しいプロファイルを追記しました。★★★");
        Console.WriteLine($"新プロファイル名: {newProfile.Name}");
    }
}