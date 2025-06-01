using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace backend.Tests.TestHelpers;

public class FakeProtector : IDataProtector
{
    public string ToReturnOnUnprotect { get; set; } = "";

    public byte[] Protect(byte[] plaintext)
    {
        // Just return the plaintext with a "fake" prefix
        var protectedText = "[encrypted]" + Encoding.UTF8.GetString(plaintext);
        return Encoding.UTF8.GetBytes(protectedText);
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        var protectedString = Encoding.UTF8.GetString(protectedData);

        if (protectedString == "invalid-token")
            throw new CryptographicException("Tampered token");

        return Encoding.UTF8.GetBytes(ToReturnOnUnprotect);
    }

    public IDataProtector CreateProtector(string purpose) => this;
}
