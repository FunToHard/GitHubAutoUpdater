using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GitHubAutoUpdater.Services;
using Xunit;

namespace GitHubAutoUpdater.Tests
{
    public class ChecksumVerifierTests
    {
        [Fact]
        public void ComputeSha256_ComputesCorrectHash()
        {
            byte[] data = Encoding.UTF8.GetBytes("Hello World Update");
            using var stream = new MemoryStream(data);
            string hash = ChecksumVerifier.ComputeSha256(stream);

            using var sha256 = SHA256.Create();
            string expected = Convert.ToHexString(sha256.ComputeHash(data)).ToLowerInvariant();

            Assert.Equal(expected, hash);
        }

        [Fact]
        public async Task VerifyChecksumAsync_ReturnsTrueForMatchingHash()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, "Test Content for Verification");
                string expectedHash = await ChecksumVerifier.ComputeSha256Async(tempFile);

                bool result = await ChecksumVerifier.VerifyChecksumAsync(tempFile, expectedHash);
                Assert.True(result);

                bool caseInsensitiveResult = await ChecksumVerifier.VerifyChecksumAsync(tempFile, expectedHash.ToUpperInvariant());
                Assert.True(caseInsensitiveResult);

                bool wrongHashResult = await ChecksumVerifier.VerifyChecksumAsync(tempFile, "0000000000000000000000000000000000000000000000000000000000000000");
                Assert.False(wrongHashResult);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExtractChecksumFromText_GnuFormat_ExtractsCorrectly()
        {
            string content = @"
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  otherfile.zip
4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945 *DockAll-Standalone-win-x64.zip
";
            string? hash = ChecksumVerifier.ExtractChecksumFromText(content, "DockAll-Standalone-win-x64.zip");
            Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", hash);
        }

        [Fact]
        public void ExtractChecksumFromText_PowerShellFormatList_ExtractsCorrectly()
        {
            string content = @"
Algorithm : SHA256
Hash      : 4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945
Path      : F:\DEV\dockall\artifacts\DockAll-Standalone-win-x64.zip

Algorithm : SHA256
Hash      : E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
Path      : F:\DEV\dockall\artifacts\DockAll-FrameworkDependent-win-x64.zip
";
            string? hash = ChecksumVerifier.ExtractChecksumFromText(content, "DockAll-Standalone-win-x64.zip");
            Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", hash);

            string? hash2 = ChecksumVerifier.ExtractChecksumFromText(content, "DockAll-FrameworkDependent-win-x64.zip");
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash2);
        }

        [Fact]
        public void ExtractChecksumFromText_SingleHash_ExtractsCorrectly()
        {
            string content = "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945\n";
            string? hash = ChecksumVerifier.ExtractChecksumFromText(content, "any-file.zip");
            Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", hash);
        }
    }
}
