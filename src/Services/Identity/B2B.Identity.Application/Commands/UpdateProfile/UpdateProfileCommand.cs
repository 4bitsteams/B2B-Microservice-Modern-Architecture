using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(
    string FirstName,
    string LastName,
    string PhoneNumber) : ICommand;
