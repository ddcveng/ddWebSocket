using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;


namespace chatApp
{
    /// Class used for encrypting user passwords
    /**
     * I needed to have some random value for each user that I can use for deterministic
     * encryption so I can compare the passwords on login. For this I chose
     * the time of registration, but that's stored in plaintext so if someone knows
     * how the passwords are encrypted it doesnt make much of a difference.
     *
     * modfied from this example
     * https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=netcore-3.1
     *
     * not really secure but whatever ¯\_(ツ)_/¯
     */
    public static class AesEncryptor
    {
        public static void Encrypt(User user)
        {
            user.Password = Convert.ToBase64String(_Encrypt(user.Password, user.DateCreated));
        }
        private static byte[] _Encrypt(string plaintext, string key)
        {
            byte[] encrypted;
            byte[] Key;
            byte[] IV256;
            byte[] IV128 = new byte[16];
            key = (Convert.ToDateTime(key) - (new DateTime(1970, 1, 1))).TotalSeconds.ToString();

            using (SHA256 mySHA256 = SHA256.Create())
            {
                Key = mySHA256.ComputeHash(Encoding.ASCII.GetBytes(key));
                IV256 = mySHA256.ComputeHash(Key);
            }

            Array.Copy(IV256, IV128, 16);

            using (Aes myAes = Aes.Create())
            {
                myAes.Key = Key;
                myAes.IV = IV128;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = myAes.CreateEncryptor(myAes.Key, myAes.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plaintext);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            return encrypted;
        }

        /// Check if the provided password matches the user's
        /**
         * Encrypts the provided password and compares the resulting value to
         * the stored password
         */
        public static bool Compare(string password, User user)
        {
            byte[] passBytes = _Encrypt(password, user.DateCreated);
            string passEncrypted = Convert.ToBase64String(passBytes);
            return passEncrypted == user.Password;
        }
    }
}
