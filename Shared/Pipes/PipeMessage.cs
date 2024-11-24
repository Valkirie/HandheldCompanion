namespace Shared.Pipes
{
    public abstract class PipeMessage
    {
        public PipeCode code;

        [Serializable]
        public partial class PipeServerPing : PipeMessage
        {
            public PipeServerPing()
            {
                code = PipeCode.SERVER_PING;
            }
        }
    }
}
