namespace Playr.Application.Profiles;

public interface IPublicLookingForGameService
{
    Task<PublicLookingForGameSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
