using POS.Application.DTOs;

namespace POS.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);
}