namespace Apps.MicrosoftOneDrive.Dtos;

public class ErrorDto
{
    public ErrorDetails Error { get; set; }
}

public class ErrorDetails
{
    public string Code { get; set; }
    public string Message { get; set; }
}