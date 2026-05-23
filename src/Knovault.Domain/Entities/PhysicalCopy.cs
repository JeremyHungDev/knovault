namespace Knovault.Domain.Entities;

public class PhysicalCopy : BookCopy
{
    public string? Location { get; private set; }
    public DateOnly? AcquiredDate { get; private set; }

    public PhysicalCopy(string? location = null, DateOnly? acquiredDate = null)
    {
        Location = location;
        AcquiredDate = acquiredDate;
    }

    public void UpdateLocation(string? location) => Location = location;
    public void SetAcquiredDate(DateOnly? date) => AcquiredDate = date;
}
