// Purpose: Defines ToolBridgeMessage.
using System.Text.Json;

namespace Dip.Agent.Core;

public sealed record ToolBridgeMessage(string? Token, ToolEventObservation Event);
