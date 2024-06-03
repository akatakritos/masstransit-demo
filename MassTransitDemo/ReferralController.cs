using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace MassTransitDemo;

[ApiController]
[Route("api/referrals")]
public class ReferralController: ControllerBase
{
    private readonly ILogger<ReferralController> _log;
    private readonly AppDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public ReferralController(ILogger<ReferralController> log, AppDbContext db, IPublishEndpoint publishEndpoint)
    {
        _log = log;
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    public async Task<IActionResult> SendReferral()
    {
        var faker = new Bogus.Faker();
        var referral = Referral.Create(faker.Name.FullName());
        _db.Referrals.Add(referral);
        
        var sendReferralCommand = new DeliverReferralCmd(referral.Key);
        _log.LogInformation("Enqueuing referral key {ReferralKey}", sendReferralCommand.ReferralKey);
        await _publishEndpoint.Publish(sendReferralCommand);
        
        _log.LogInformation("Saving changes...");
        await _db.SaveChangesAsync(); // save and send
        
        _log.LogInformation("HTTP request done");
        return Ok();
    }
}