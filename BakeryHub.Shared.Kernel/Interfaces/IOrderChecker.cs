namespace BakeryHub.Shared.Kernel.Interfaces;
public interface IOrderChecker
{
    Task<bool> IsProductInActiveOrderAsync(Guid productId);
}
