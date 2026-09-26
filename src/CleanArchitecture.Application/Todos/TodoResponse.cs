using CleanArchitecture.Domain.Todos;

namespace CleanArchitecture.Application.Todos;

public sealed class TodoResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Description { get; init; }
    public DateTime? DueDate { get; init; }
    public List<string> Labels { get; init; }
    public Priority Priority { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
