namespace InkWell.Shared;

// base class that every db entity inherits from
// kept audit fields here so we dont repeat them everywhere
public abstract class BaseEntity
{
    // using Guid.NewGuid() as default so efcore doesnt need to generate it
    public Guid Id { get; set; } = Guid.NewGuid();

    // track when record was first created, always utc
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // nullable because new records wont have an update yet
    public DateTime? UpdatedAt { get; set; }

    // soft delete flag - we never hard delete records
    public bool IsDeleted { get; set; } = false;
}
