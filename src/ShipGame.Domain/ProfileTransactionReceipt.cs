namespace ShipGame.Domain;

public sealed record ProfileTransactionReceipt(string TransactionId, string Operation, ulong Fingerprint);
