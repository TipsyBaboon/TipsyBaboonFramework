using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.Core.FrameworkBase;

namespace TipsyBaboonTestSite.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly BaseModelStore _store;
        private readonly IPasswordHasher _passwordHasher;

        public AuthenticationService(
            BaseModelStore store,
            IPasswordHasher passwordHasher)
        {
            _store = store;
            _passwordHasher = passwordHasher;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await FindByEmailAsync(email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                return null;
            }

            var isValid = await ValidatePasswordAsync(password, user.PasswordHash);
            return isValid ? user : null;
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            var normalizedEmail = email.ToUpperInvariant();
            var users = await _store.QueryTypedAsync<User>(u => u.NormalizedEmail == normalizedEmail);
            return users.FirstOrDefault();
        }

        public async Task<User?> FindByIdAsync(Guid userId)
        {
            return await _store.GetTypedByIdAsync<User>(userId);
        }

        public async Task<User> CreateUserAsync(string email, string? displayName = null, string? password = null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email, // Use email as username
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = displayName ?? email,
                EmailConfirmed = true, // Auto-confirm for OAuth users
                IsInvariant = false
            };

            if (!string.IsNullOrEmpty(password))
            {
                user.PasswordHash = _passwordHasher.HashPassword(password);
            }

            await _store.InsertTypedAsync<User>(user);

            return user;
        }

        public async Task<UserLogin?> FindUserLoginAsync(string loginProvider, string providerKey)
        {
            var logins = await _store.QueryTypedAsync<UserLogin>(
                l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey);
            return logins.FirstOrDefault();
        }

        public async Task AddUserLoginAsync(Guid userId, string loginProvider, string providerKey, string? providerDisplayName = null)
        {
            var userLogin = new UserLogin
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LoginProvider = loginProvider,
                ProviderKey = providerKey,
                ProviderDisplayName = providerDisplayName,
                IsInvariant = false
            };

            await _store.InsertTypedAsync<UserLogin>(userLogin);
        }

        public Task<bool> ValidatePasswordAsync(string password, string hashedPassword)
        {
            return Task.FromResult(_passwordHasher.VerifyPassword(hashedPassword, password));
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await FindByIdAsync(userId)
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            if (string.IsNullOrEmpty(user.PasswordHash))
                throw new InvalidOperationException($"User '{userId}' has no password set");

            var isValid = await ValidatePasswordAsync(currentPassword, user.PasswordHash);
            if (!isValid)
            {
                return false;
            }

            user.PasswordHash = _passwordHasher.HashPassword(newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString(); // Invalidate existing sessions
            
            await _store.UpdateTypedAsync<User>(user);
            return true;
        }

        public Task<string> GenerateAuthenticatorKeyAsync()
        {
            var key = new byte[20];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            
            return Task.FromResult(Base32Encode(key));
        }

        public async Task<bool> EnableTwoFactorAsync(Guid userId, string authenticatorKey, string verificationCode)
        {
            var user = await FindByIdAsync(userId)
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            if (!await VerifyTwoFactorCodeAsync(authenticatorKey, verificationCode))
            {
                return false;
            }

            user.TwoFactorEnabled = true;
            user.SecurityStamp = authenticatorKey; // TODO: Use proper storage mechanism
            
            await _store.UpdateTypedAsync<User>(user);
            return true;
        }

        public async Task<bool> DisableTwoFactorAsync(Guid userId)
        {
            var user = await FindByIdAsync(userId)
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            user.TwoFactorEnabled = false;
            user.SecurityStamp = null;
            
            await _store.UpdateTypedAsync<User>(user);
            return true;
        }

        public Task<bool> VerifyTwoFactorCodeAsync(string authenticatorKey, string code)
        {
            if (string.IsNullOrEmpty(authenticatorKey) || string.IsNullOrEmpty(code))
            {
                return Task.FromResult(false);
            }

            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeStep = unixTimestamp / 30;

            for (int i = -1; i <= 1; i++)
            {
                var totp = GenerateTOTP(authenticatorKey, timeStep + i);
                if (totp == code)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        private string GenerateTOTP(string secret, long timeStep)
        {
            var keyBytes = Base32Decode(secret);
            var timeBytes = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timeBytes);
            }

            using (var hmac = new System.Security.Cryptography.HMACSHA1(keyBytes))
            {
                var hash = hmac.ComputeHash(timeBytes);
                var offset = hash[hash.Length - 1] & 0x0F;
                var binary = ((hash[offset] & 0x7F) << 24)
                    | ((hash[offset + 1] & 0xFF) << 16)
                    | ((hash[offset + 2] & 0xFF) << 8)
                    | (hash[offset + 3] & 0xFF);

                var otp = binary % 1000000;
                return otp.ToString("D6");
            }
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var result = new System.Text.StringBuilder();
            var bits = 0;
            var bitBuffer = 0;

            foreach (var b in data)
            {
                bitBuffer = (bitBuffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    result.Append(alphabet[(bitBuffer >> (bits - 5)) & 0x1F]);
                    bits -= 5;
                }
            }

            if (bits > 0)
            {
                result.Append(alphabet[(bitBuffer << (5 - bits)) & 0x1F]);
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string encoded)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var result = new List<byte>();
            var bits = 0;
            var bitBuffer = 0;

            foreach (var c in encoded.ToUpperInvariant())
            {
                var value = alphabet.IndexOf(c);
                if (value < 0) continue;

                bitBuffer = (bitBuffer << 5) | value;
                bits += 5;

                if (bits >= 8)
                {
                    result.Add((byte)((bitBuffer >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }

            return result.ToArray();
        }

        public async Task UpdateDisplayNameAsync(Guid userId, string? displayName)
        {
            var user = await FindByIdAsync(userId);
            if (user == null)
            {
                return;
            }

            user.DisplayName = displayName;
            await _store.UpdateTypedAsync<User>(user);
        }
    }
}
