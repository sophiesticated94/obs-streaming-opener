namespace ObsStreamingOpener.Domain;

public enum TipKind
{
    Donation = 1,
    PatronPayment = 2,
    CampaignDonation = 3,
    Refund = 4,
    Chargeback = 5,
    Payout = 6,
    PayoutFee = 7,
    Adjustment = 8
}
