using POS.Application.DTOs;

namespace POS.Application.Interfaces;

public interface IPosService
{
    Task<ReceiptResultDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default);
}