using BusinessObjects.DataTransferObjects.AuthDTOs;
using BusinessObjects.DataTransferObjects.Googles;
using BusinessObjects.Domain;
using DataAccessObjects.DAO;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Repositories.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly ITokenRepository _tokenRepository;

        private readonly string _googleClientId;

        public AuthRepository(IOptions<GoogleSettings> googleSettings , ITokenRepository tokenRepository)
        {
            _googleClientId = googleSettings.Value.ClientId;
            _tokenRepository = tokenRepository;
        }


        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            // Tìm user theo email
            var user = await UserDAO.GetUserByEmail(request.Email);

            if (user == null)
            {
                return null; 
            }

            if (user.Password != request.Password)
            {
                return null;
            }

            // Tạo JWT token
            var token = _tokenRepository.GenerateJwtToken(user);

            return new LoginResponse
            {
                UserId = user.UserId,
                FullName = user.FullName ?? "",
                Email = user.Email,
                Role = user.Role ?? "Patient",
                Token = token,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            }; ;
        }

        public async Task<LoginResponse?> LoginGoogleAsync(LoginGoogle request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.IdToken) || string.IsNullOrWhiteSpace(_googleClientId))
                {
                    return null;
                }

                //  Xác minh idToken với Google
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _googleClientId }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

                //  Lấy email từ token
                var email = payload.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    return null;
                }

                // Kiểm tra tài khoản hiện có theo email
                var user = await UserDAO.GetUserByEmail(email);
                if (user == null)
                {
                    // Auto-provision user mới khi đăng nhập Google lần đầu
                    var displayName = string.IsNullOrWhiteSpace(payload.Name) ? email : payload.Name;
                    var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                    user = await UserDAO.CreateUserAsync(new User
                    {
                        Email = email,
                        Password = temporaryPassword,
                        FullName = displayName,
                        Role = "Patient",
                        AvatarUrl = payload.Picture,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    });

                    if (user == null)
                    {
                        return null;
                    }
                }

                //Nếu tồn tại → sinh JWT token bình thường
                var token = _tokenRepository.GenerateJwtToken(user);

                return new LoginResponse
                {
                    UserId = user.UserId,
                    FullName = user.FullName ?? email,
                    Email = user.Email,
                    Role = user.Role ?? "Patient",
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    AvatarUrl = user.AvatarUrl ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LoginGoogleAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra email đã tồn tại chưa
            var existingUser = await UserDAO.GetUserByEmail(request.Email);
            if (existingUser != null)
            {
                return null; // Email đã tồn tại
            }

            // Tạo user mới
            var newUser = new User
            {
                Email = request.Email,
                Password = request.Password, 
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role ?? "Patient",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createdUser = await UserDAO.CreateUserAsync(newUser);
            if (createdUser == null)
            {
                return null; 
            }

            return new RegisterResponse
            {
                UserId = createdUser.UserId,
                Email = createdUser.Email,
                FullName = createdUser.FullName,
                Role = createdUser.Role,
                PhoneNumber = createdUser.PhoneNumber,
                CreatedAt = createdUser.CreatedAt ?? DateTime.UtcNow,
                Message = "User registered successfully"
            };
        }
    }
}
