namespace MassTransitDemo;

public enum ReferralStatus: short
{
    Submitted = 1,
    Delivered = 2
}

public class Referral
{
    public Guid Key { get; set; }
    public string Name { get; set; }
    public ReferralStatus Status { get; set; }

    public static Referral Create(string name)
    {
        return new Referral()
        {
            Key = Guid.NewGuid(),
            Name = name,
            Status = ReferralStatus.Submitted
        };
    }
}