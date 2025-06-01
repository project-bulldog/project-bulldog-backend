using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace backend.Tests.TestHelpers;

public class FakeProtector : IDataProtector
{
    public string? ToReturnOnUnprotect { get; set; }
    public bool ThrowOnUnprotect { get; set; }

    public IDataProtector CreateProtector(string purpose)
    {
        return this;
    }

    public byte[] Protect(byte[] plaintext)
    {
        return plaintext;
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        if (ThrowOnUnprotect)
        {
            throw new Exception("Simulated protection failure");
        }
        return protectedData;
    }
}
