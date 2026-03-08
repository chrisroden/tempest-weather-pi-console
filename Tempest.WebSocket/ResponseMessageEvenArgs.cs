using Tempest.WebSocket.Models.Responses;

public class ResponseMessageEvenArgs : EventArgs
{
    public ResponseMessageEvenArgs(ResponseMessageBase responseMessage)
    {
        ResponseMessage = responseMessage;
    }

    public ResponseMessageBase ResponseMessage { get; set; }
}