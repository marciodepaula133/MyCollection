// Generates a local RS256 PEM keypair for dev (AD-6, AD-17). Never committed - see .gitignore.
// A .NET 10 file-based app, so only the .NET SDK is needed (no openssl). Run it through
// tools/generate-dev-jwt-key.sh or .ps1, which pass <repo>/keys as the output directory:
//   dotnet run --file tools/generate-dev-jwt-key.cs -- <output-dir>
using System.Security.Cryptography;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --file tools/generate-dev-jwt-key.cs -- <output-dir>");
    return 2;
}

var outDir = Path.GetFullPath(args[0]);
var privateKeyPath = Path.Combine(outDir, "jwt-signing-key.pem");
var publicKeyPath = Path.Combine(outDir, "jwt-signing-key.pub.pem");

Directory.CreateDirectory(outDir);

if (File.Exists(privateKeyPath))
{
    Console.WriteLine($"Key already exists at {privateKeyPath} - remove it first if you want a new one.");
    return 0;
}

using var rsa = RSA.Create(2048);

// PKCS#8 private key ("BEGIN PRIVATE KEY") and SPKI public key ("BEGIN PUBLIC KEY"): the same
// formats `openssl genpkey` / `openssl rsa -pubout` produced before.
File.WriteAllText(privateKeyPath, rsa.ExportPkcs8PrivateKeyPem() + "\n");
File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem() + "\n");

Console.WriteLine("Generated dev JWT signing key:");
Console.WriteLine($"  Private: {privateKeyPath}  (set Jwt__SigningKeyPath to this path)");
Console.WriteLine($"  Public:  {publicKeyPath}");
Console.WriteLine("These are gitignored (*.pem) - never commit them.");
return 0;
