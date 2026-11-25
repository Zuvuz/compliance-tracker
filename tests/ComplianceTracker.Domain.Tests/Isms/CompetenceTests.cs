using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-213: competences, training records, people without a user and policy confirmations. Every test follows
// Arrange / Act / Assert.
public class CompetenceTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 21);

    private static Competence Awareness(int? months = 12, bool people = false)
        => Competence.Create(Organisation, "Security awareness", months, [UserRole.Developer, UserRole.ComplianceManager], people, Monday);

    private static TrainingRecord Trained(Competence competence, DateOnly on)
        => TrainingRecord.Create(Organisation, competence, Bo, null, on, null, Bo, Today, Monday);

    // ---------------------------------------------------------- R1

    [Fact]
    public void A_competence_is_required_by_its_roles()
    {
        var competence = Awareness();

        Assert.True(competence.IsRequiredFor(UserRole.Developer));
        Assert.False(competence.IsRequiredFor(UserRole.Auditor));
        Assert.False(competence.IsRequiredFor(null));
    }

    [Fact]
    public void A_competence_can_be_for_people_without_a_user()
        => Assert.True(Competence.Create(Organisation, "Clean desk", null, [], true, Monday).IsRequiredFor(null));

    [Fact]
    public void A_competence_needs_a_name_and_someone_who_needs_it()
    {
        Assert.Throws<DomainException>(() => Competence.Create(Organisation, " ", 12, [UserRole.Developer], false, Monday));
        Assert.Throws<DomainException>(() => Competence.Create(Organisation, "Awareness", 12, [], false, Monday));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void A_renewal_is_every_1_to_60_months(int months)
        => Assert.Throws<DomainException>(() => Awareness(months));

    // ---------------------------------------------------------- R2

    [Fact]
    public void A_yearly_training_expires_on_its_anniversary()
    {
        var competence = Awareness();
        var record = Trained(competence, new DateOnly(2025, 10, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), record.ValidUntil);
        Assert.True(record.IsValidOn(new DateOnly(2026, 9, 30)));
        Assert.False(record.IsValidOn(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void An_expired_training_of_a_required_competence_is_expired()
    {
        var competence = Awareness();

        var state = CompetenceMatrix.StateOf(competence, UserRole.Developer, [Trained(competence, new DateOnly(2025, 10, 1))], Today);

        Assert.Equal(CompetenceState.Expired, state);
    }

    [Fact]
    public void The_newest_training_decides()
    {
        var competence = Awareness();

        var state = CompetenceMatrix.StateOf(competence, UserRole.Developer,
            [Trained(competence, new DateOnly(2025, 10, 1)), Trained(competence, new DateOnly(2026, 11, 1))], Today);

        Assert.Equal(CompetenceState.Valid, state);
    }

    [Fact]
    public void A_competence_without_renewal_stays_valid()
    {
        var competence = Awareness(null);

        Assert.Equal(CompetenceState.Valid, CompetenceMatrix.StateOf(competence, UserRole.Developer, [Trained(competence, new DateOnly(2015, 1, 1))], Today));
    }

    [Fact]
    public void A_required_competence_without_training_is_missing_and_one_not_required_is_not()
    {
        var competence = Awareness();

        Assert.Equal(CompetenceState.Missing, CompetenceMatrix.StateOf(competence, UserRole.Developer, [], Today));
        Assert.Equal(CompetenceState.NotRequired, CompetenceMatrix.StateOf(competence, UserRole.Auditor, [], Today));
    }

    [Fact]
    public void A_training_needs_one_person_and_a_date_not_in_the_future()
    {
        var competence = Awareness();

        Assert.Throws<DomainException>(() => TrainingRecord.Create(Organisation, competence, null, null, Today, null, Bo, Today, Monday));
        Assert.Throws<DomainException>(() => TrainingRecord.Create(Organisation, competence, Bo, Guid.NewGuid(), Today, null, Bo, Today, Monday));
        Assert.Throws<DomainException>(() => TrainingRecord.Create(Organisation, competence, Bo, null, Today.AddDays(1), null, Bo, Today, Monday));
        Assert.Throws<DomainException>(() => TrainingRecord.Create(Organisation, competence, Bo, null, null, null, Bo, Today, Monday));
    }

    [Fact]
    public void A_removed_competence_takes_no_training()
    {
        var competence = Awareness();
        competence.Remove(Monday);

        Assert.Throws<DomainException>(() => Trained(competence, Today));
    }

    // ---------------------------------------------------------- R3

    [Fact]
    public void A_new_version_must_be_read_again()
    {
        var acknowledgement = PolicyAcknowledgement.Confirm(Organisation, Guid.NewGuid(), Bo, "3", Monday);

        Assert.True(CompetenceMatrix.HasRead("3", [acknowledgement]));
        Assert.False(CompetenceMatrix.HasRead("4", [acknowledgement]));
    }

    // ---------------------------------------------------------- R5

    [Fact]
    public void A_person_without_a_user_needs_a_name_and_is_removed_once()
    {
        Assert.Throws<DomainException>(() => StaffPerson.Create(Organisation, "  ", Monday));
        var person = StaffPerson.Create(Organisation, "Kim (cleaning)", Monday);

        person.Remove(Monday);

        Assert.False(person.IsActive);
        Assert.Throws<DomainException>(() => person.Remove(Monday));
    }
}
