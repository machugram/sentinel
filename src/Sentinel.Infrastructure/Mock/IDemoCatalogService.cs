namespace Sentinel.Infrastructure.Mock;

public interface IDemoCatalogService
{
    event EventHandler? CatalogChanged;
    void ResetToSeed();
}
