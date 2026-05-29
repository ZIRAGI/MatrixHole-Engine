using System;
using System.Security.Cryptography;

var rsa = RSA.Create(2048);
var pub = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
var priv = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

Console.WriteLine("PUBLIC_KEY_START");
Console.WriteLine(pub);
Console.WriteLine("PUBLIC_KEY_END");
Console.WriteLine("PRIVATE_KEY_START");
Console.WriteLine(priv);
Console.WriteLine("PRIVATE_KEY_END");
