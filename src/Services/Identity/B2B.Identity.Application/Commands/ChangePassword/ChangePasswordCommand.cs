using System.Text.Json.Serialization;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    [property: JsonIgnore] string CurrentPassword,   // excluded from AuditBehavior serialization
    [property: JsonIgnore] string NewPassword,
    [property: JsonIgnore] string ConfirmNewPassword) : ICommand;
