namespace Agender.Shared.Services;

public class NotFoundException(string message) : Exception(message);
