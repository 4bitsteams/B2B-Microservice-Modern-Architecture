using B2B.Shared.Core.CQRS;

namespace B2B.Shipping.Application.Commands.UpdateTrackingInfo;

public sealed record UpdateTrackingInfoCommand(Guid ShipmentId, string NewTrackingNumber) : ICommand<UpdateTrackingInfoResponse>;
public sealed record UpdateTrackingInfoResponse(Guid ShipmentId, string TrackingNumber);
