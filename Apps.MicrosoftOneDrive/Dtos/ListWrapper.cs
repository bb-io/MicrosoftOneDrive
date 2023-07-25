namespace Apps.MicrosoftOneDrive.Dtos;

public class ListWrapper<T>
{
    public IEnumerable<T> Value { get; set; }
}