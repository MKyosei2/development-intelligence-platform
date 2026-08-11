using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using StackExchange.Redis;
namespace Dip.Infrastructure.Messaging;
public sealed class RedisQueueClient : IQueueClient
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    public RedisQueueClient(IConnectionMultiplexer redis)=>_redis=redis;
    public async Task PublishAsync<T>(string stream,T message,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentException.ThrowIfNullOrWhiteSpace(stream); ArgumentNullException.ThrowIfNull(message);
        var payload=JsonSerializer.Serialize(message,JsonOptions); var type=ReadScalar(payload,"messageType")??typeof(T).FullName??typeof(T).Name;
        var correlation=ReadScalar(payload,"correlationId")??string.Empty; _=int.TryParse(ReadScalar(payload,"attempt"),out var attempt);
        await AddEnvelopeAsync(stream,type,payload,correlation,Math.Max(0,attempt),cancellationToken);
    }
    public async Task<QueueEnvelope?> ReadAsync(string stream,string consumerGroup,string consumerName,TimeSpan block,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); if(block<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(block));
        var db=_redis.GetDatabase();
        try{await db.StreamCreateConsumerGroupAsync(stream,consumerGroup,"0-0",true);}catch(RedisServerException ex)when(ex.Message.Contains("BUSYGROUP",StringComparison.OrdinalIgnoreCase)){}
        var deadline=DateTimeOffset.UtcNow+block;
        while(!cancellationToken.IsCancellationRequested)
        {
            var entries=await db.StreamReadGroupAsync(stream,consumerGroup,consumerName,">",1);
            if(entries.Length>0){var e=entries[0];var v=e.Values.ToDictionary(x=>x.Name.ToString(),x=>x.Value.ToString(),StringComparer.OrdinalIgnoreCase);_=int.TryParse(v.GetValueOrDefault("attempt"),out var attempt);return new(e.Id.ToString(),v.GetValueOrDefault("messageType")??"unknown",v.GetValueOrDefault("payload")??"{}",Math.Max(0,attempt),v.GetValueOrDefault("correlationId")??string.Empty);}
            if(DateTimeOffset.UtcNow>=deadline)return null; await Task.Delay(100,cancellationToken);
        }
        return null;
    }
    public Task AcknowledgeAsync(string stream,string consumerGroup,string messageId,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();return _redis.GetDatabase().StreamAcknowledgeAsync(stream,consumerGroup,messageId);}
    public async Task RetryAsync(string stream,string consumerGroup,QueueEnvelope message,CancellationToken cancellationToken){await AddEnvelopeAsync(stream,message.MessageType,message.PayloadJson,message.CorrelationId,checked(message.Attempt+1),cancellationToken);await AcknowledgeAsync(stream,consumerGroup,message.MessageId,cancellationToken);}
    public async Task MoveToDeadLetterAsync(string sourceStream,string consumerGroup,string deadLetterStream,QueueEnvelope message,string reason,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();var safe=string.IsNullOrWhiteSpace(reason)?"Unknown queue processing failure.":reason.Trim();if(safe.Length>4000)safe=safe[..4000];var db=_redis.GetDatabase();
        await db.StreamAddAsync(deadLetterStream,[new("sourceMessageId",message.MessageId),new("messageType",message.MessageType),new("payload",message.PayloadJson),new("attempt",message.Attempt),new("correlationId",message.CorrelationId),new("reason",safe),new("failedAtUtc",DateTimeOffset.UtcNow.ToString("O"))]);
        await db.StreamAcknowledgeAsync(sourceStream,consumerGroup,message.MessageId);
    }
    private async Task AddEnvelopeAsync(string stream,string type,string payload,string correlation,int attempt,CancellationToken ct){ct.ThrowIfCancellationRequested();await _redis.GetDatabase().StreamAddAsync(stream,[new("messageType",type),new("payload",payload),new("correlationId",correlation),new("attempt",attempt)]);}
    private static string? ReadScalar(string json,string name){try{using var d=JsonDocument.Parse(json);foreach(var p in d.RootElement.EnumerateObject()){if(!p.Name.Equals(name,StringComparison.OrdinalIgnoreCase))continue;return p.Value.ValueKind switch{JsonValueKind.String=>p.Value.GetString(),JsonValueKind.Number=>p.Value.GetRawText(),JsonValueKind.True=>"true",JsonValueKind.False=>"false",_=>null};}}catch(JsonException){}return null;}
}
