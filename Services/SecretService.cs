using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TransparentCaptureApp.Utilities;

namespace TransparentCaptureApp.Services;

public sealed class SecretService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TransparentCaptureApp.v1");
    private readonly string _secretsFilePath = Path.Combine(PathUtility.AppDataDirectory, "secrets.dat");

    public string GetSecret(string key)
    {
        var secrets = LoadSecrets();
        return secrets.TryGetValue(key, out var value) ? value : "";
    }

    public void SetSecret(string key, string value)
    {
        var secrets = LoadSecrets();
        secrets[key] = value;
        SaveSecrets(secrets);
    }

    private Dictionary<string, string> LoadSecrets()
    {
        if (!File.Exists(_secretsFilePath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_secretsFilePath);
            var bytes = Dpapi.Unprotect(protectedBytes, Entropy);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private void SaveSecrets(Dictionary<string, string> secrets)
    {
        Directory.CreateDirectory(PathUtility.AppDataDirectory);
        var json = JsonSerializer.Serialize(secrets);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = Dpapi.Protect(bytes, Entropy);
        File.WriteAllBytes(_secretsFilePath, protectedBytes);
    }

    private static class Dpapi
    {
        private const int CryptProtectUiForbidden = 0x1;

        public static byte[] Protect(byte[] plainBytes, byte[] entropy)
        {
            return Execute(plainBytes, entropy, protect: true);
        }

        public static byte[] Unprotect(byte[] protectedBytes, byte[] entropy)
        {
            return Execute(protectedBytes, entropy, protect: false);
        }

        private static byte[] Execute(byte[] input, byte[] entropy, bool protect)
        {
            var inputBlob = CreateBlob(input);
            var entropyBlob = CreateBlob(entropy);
            var outputBlob = new DataBlob();

            try
            {
                var ok = protect
                    ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob)
                    : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob);

                if (!ok)
                {
                    throw new InvalidOperationException("DPAPI処理に失敗しました。");
                }

                var output = new byte[outputBlob.cbData];
                Marshal.Copy(outputBlob.pbData, output, 0, output.Length);
                return output;
            }
            finally
            {
                FreeBlob(inputBlob);
                FreeBlob(entropyBlob);
                if (outputBlob.pbData != IntPtr.Zero)
                {
                    LocalFree(outputBlob.pbData);
                }
            }
        }

        private static DataBlob CreateBlob(byte[] bytes)
        {
            var blob = new DataBlob
            {
                cbData = bytes.Length,
                pbData = Marshal.AllocHGlobal(bytes.Length)
            };
            Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
            return blob;
        }

        private static void FreeBlob(DataBlob blob)
        {
            if (blob.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(blob.pbData);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DataBlob pDataIn,
            string? szDataDescr,
            ref DataBlob pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DataBlob pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DataBlob pDataIn,
            IntPtr ppszDataDescr,
            ref DataBlob pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            ref DataBlob pDataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
