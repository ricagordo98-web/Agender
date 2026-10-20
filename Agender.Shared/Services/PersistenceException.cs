namespace Agender.Shared.Services;

public class PersistenceException(string message, Exception? innerException = null) : Exception(message, innerException);
