namespace FileSyncApp.Core.Interfaces;

public interface ICredentialService
{
    void SaveRefreshToken(string username, string refreshToken);
    (string Username, string Token)? LoadRefreshToken();
    byte[] Protect(string plainText);
    string Unprotect(byte[] data);
}
