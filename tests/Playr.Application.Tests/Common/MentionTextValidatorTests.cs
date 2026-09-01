using FluentAssertions;
using Playr.Application.Common;

namespace Playr.Application.Tests.Common;

public sealed class MentionTextValidatorTests
{
    [Fact]
    public void FilterPresentInText_keeps_user_whose_username_is_an_exact_token_in_the_text()
    {
        var candidates = new[] { (UserId: Guid.NewGuid(), Username: "TyyD") };

        var result = MentionTextValidator.FilterPresentInText("hello @TyyD", candidates);

        result.Should().BeEquivalentTo(candidates.Select(c => c.UserId));
    }

    [Fact]
    public void FilterPresentInText_drops_user_whose_username_is_only_a_substring_of_a_longer_token()
    {
        // "Ty" is a prefix of the actual "@TyyD" token in the text, but was never itself typed.
        var staleCandidate = (UserId: Guid.NewGuid(), Username: "Ty");

        var result = MentionTextValidator.FilterPresentInText("hello @TyyD", [staleCandidate]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterPresentInText_is_case_insensitive()
    {
        var candidates = new[] { (UserId: Guid.NewGuid(), Username: "TyyD") };

        var result = MentionTextValidator.FilterPresentInText("hello @tyyd", candidates);

        result.Should().BeEquivalentTo(candidates.Select(c => c.UserId));
    }
}
