namespace Repository.Exceptions;

/// <summary>
/// The database refused a save (a duplicate key, a broken reference). Thrown by the repositories in place of the Entity Framework exception, so the
/// layers above do not need to know which data access library is underneath. The original exception is kept as <see cref="Exception.InnerException"/>.
/// </summary>
public class DataConflictException : Exception
{
    public DataConflictException(Exception inner) : base(inner.Message, inner)
    {
    }
}

/// <summary>A save was refused because somebody else changed the same row first (optimistic concurrency). A kind of <see cref="DataConflictException"/>.</summary>
public class ConcurrencyConflictException : DataConflictException
{
    public ConcurrencyConflictException(Exception inner) : base(inner)
    {
    }
}
