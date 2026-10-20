using Microsoft.AspNetCore.Identity;

namespace Agender.Infrastructure.Identity;

public class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Create(nameof(DefaultError), "Ocorreu um erro inesperado.");

    public override IdentityError DuplicateEmail(string email) => Create(nameof(DuplicateEmail), "Já existe uma conta com este email.");

    public override IdentityError DuplicateUserName(string userName) => Create(nameof(DuplicateUserName), "Já existe uma conta com este email.");

    public override IdentityError InvalidEmail(string? email) => Create(nameof(InvalidEmail), "O email não é válido.");

    public override IdentityError InvalidUserName(string? userName) => Create(nameof(InvalidUserName), "O email não é válido.");

    public override IdentityError PasswordMismatch() => Create(nameof(PasswordMismatch), "A palavra-passe atual está incorreta.");

    public override IdentityError PasswordTooShort(int length) => Create(nameof(PasswordTooShort), $"A palavra-passe tem de ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresDigit() => Create(nameof(PasswordRequiresDigit), "A palavra-passe tem de ter pelo menos um número.");

    public override IdentityError PasswordRequiresLower() => Create(nameof(PasswordRequiresLower), "A palavra-passe tem de ter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper() => Create(nameof(PasswordRequiresUpper), "A palavra-passe tem de ter pelo menos uma letra maiúscula.");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Create(nameof(PasswordRequiresNonAlphanumeric), "A palavra-passe tem de ter pelo menos um símbolo.");

    private static IdentityError Create(string code, string description) => new() { Code = code, Description = description };
}
