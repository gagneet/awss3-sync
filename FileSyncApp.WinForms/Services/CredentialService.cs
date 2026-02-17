using System.Security.Cryptography;
using System.Text;
using FileSyncApp.Core.Interfaces;
using Meziantou.Framework.Win32;

namespace FileSyncApp.WinForms.Services;

public sealed class CredentialService : ICredentialService
{
    private const string Target = "FileSyncApp-S3Sync-CognitoTokens";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FileSyncApp-S3Sync-v1");

    public void SaveRefreshToken(string username, string refreshToken)
    {
        CredentialManager.WriteCredential(
            applicationName: Target,
            userName: username,
            secret: refreshToken,
            persistence: CredentialPersistence.LocalMachine);
    }

    public (string Username, string Token)? LoadRefreshToken()
    {
        var cred = CredentialManager.ReadCredential(Target);
        return cred == null ? null : (cred.UserName, cred.Password);
    }

    public byte[] Protect(string plainText)
    {
        byte[] data = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    public string Unprotect(byte[] data)
    {
        byte[] plainData = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainData);
    }
}
