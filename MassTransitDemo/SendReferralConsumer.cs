using MassTransit;

namespace MassTransitDemo;

public record DeliverReferralCmd(Guid ReferralKey);

public class DeliverReferralConsumer: IConsumer<DeliverReferralCmd>
{
    private readonly ILogger<DeliverReferralConsumer> _logger;
    private readonly AppDbContext _db;

    public DeliverReferralConsumer(ILogger<DeliverReferralConsumer> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }
    
    public async Task Consume(ConsumeContext<DeliverReferralCmd> context)
    {
        _logger.LogInformation("Received referral key {ReferralKey} AttemptNumber={AttemptNumber} RedeliveryCount={RedeliveryCount}", 
            context.Message.ReferralKey, context.GetRetryAttempt(), context.GetRedeliveryCount());
        await Task.Delay(5000);
        
        var referral = await _db.Referrals.FindAsync(context.Message.ReferralKey);
        if (referral == null) throw new Exception("Failed to find the referenced referral");

        if (Random.Shared.NextDouble() > 0.75)
        {
            throw new Exception("Uhoh, network failure!");
        }
        
        referral.Status = ReferralStatus.Delivered;
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Referral key {ReferralKey} processed", context.Message.ReferralKey);
    }
}