﻿using ACMESharp.Crypto;
using Org.BouncyCastle.Crypto;
using PKISharp.WACS.Services;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using bc = Org.BouncyCastle;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    class Rsa : CsrPlugin<Rsa, RsaOptions>
    {
        private RSA _algorithm;

        public Rsa(ILogService log, RsaOptions options) : base(log, options) { }

        /// <summary>
        /// Generate CSR
        /// </summary>
        /// <param name="commonName"></param>
        /// <param name="identifiers"></param>
        /// <returns></returns>
        public override CertificateRequest GenerateCsr(X500DistinguishedName commonName)
        {
            return new CertificateRequest(commonName, Algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        /// <summary>
        /// Parameters to generate the key for
        /// </summary>
        /// <returns></returns>
        private int GetRsaKeyBits()
        {
            try
            {
                if (Properties.Settings.Default.RSAKeyBits >= 2048)
                {
                    _log.Debug("RSAKeyBits: {RSAKeyBits}", Properties.Settings.Default.RSAKeyBits);
                    return Properties.Settings.Default.RSAKeyBits;
                }
                else
                {
                    _log.Warning("RSA key bits less than 2048 is not secure.");
                }
            }
            catch (Exception ex)
            {
                _log.Warning("Unable to get RSA Key bits, error: {@ex}", ex);
            }
            return 2048;
        }

        /// <summary>
        /// Create or return algorithm
        /// </summary>
        private RSA Algorithm
        {
            get
            {
                if (_algorithm == null)
                {
                    if (_cacheData == null)
                    {
                        _cacheData = NewKeys();
                    }
                    try
                    {
                        _algorithm = CryptoHelper.Rsa.GenerateAlgorithm(_cacheData);
                    }
                    catch
                    {
                        _log.Error($"Unable to read cache data, creating new key...");
                        _cacheData = null;
                        return Algorithm;
                    }
                }
                return _algorithm;
            }
        }

        /// <summary>
        /// Create new algorithm
        /// </summary>
        /// <returns></returns>
        private string NewKeys()
        {
            var keyBits = GetRsaKeyBits();
            var rsa = RSA.Create(keyBits);
            var rsaKeys = CryptoHelper.Rsa.GenerateKeys(rsa);
            return rsaKeys; 
        }

        /// <summary>
        /// Generate or return private key
        /// </summary>
        /// <returns></returns>
        public override AsymmetricKeyParameter GetPrivateKey()
        {
            if (_algorithm == null)
            {
                throw new Exception("No Algorithm has been created yet");
            }
            var keyParams = bc.Security.DotNetUtilities.GetRsaKeyPair(_algorithm.ExportParameters(true));
            return keyParams.Private;
        }

        /// <summary>
        /// Convert to Exchange format
        /// </summary>
        /// <param name="ackp"></param>
        /// <returns></returns>
        public override AsymmetricAlgorithm Convert(AsymmetricAlgorithm ackp)
        {
            try
            {
                var cspParameters = new CspParameters
                {
                    KeyContainerName = Guid.NewGuid().ToString(),
                    KeyNumber = 1,
                    Flags = CspProviderFlags.UseMachineKeyStore,
                    ProviderType = 12 // Microsoft RSA SChannel Cryptographic Provider
                };
                var rsaProvider = new RSACryptoServiceProvider(cspParameters);
                var parameters = ((RSACryptoServiceProvider)ackp).ExportParameters(true);
                rsaProvider.ImportParameters(parameters);
                return rsaProvider;
            }
            catch (Exception ex)
            {
                // If we couldn't convert the private key that 
                // means we're left with a pfx generated with the
                // 'wrong' Crypto provider therefor delete it to 
                // make sure it's retried on the next run.
                _log.Warning("Error converting private key to Microsoft RSA SChannel Cryptographic Provider, which means it might not be usable for Exchange.");
                _log.Verbose("{ex}", ex);
                throw;
            }
        }

        public override bool CanConvert() => true;
    }
}
