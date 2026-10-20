namespace Agender.Shared.Services;

public class ServerUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
