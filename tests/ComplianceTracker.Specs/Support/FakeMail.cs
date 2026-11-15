using ComplianceTracker.Application.Reminders;

namespace ComplianceTracker.Specs.Support
{
    // US-183: a mail server that keeps the mails, and can be told to fail
    public sealed class FakeMail : IMailSender
    {
        private readonly List<(string To, string Subject, string Body)> _sent = [];

        public bool IsConfigured { get; set; } = true;
        public bool Fails { get; set; }

        public IReadOnlyList<(string To, string Subject, string Body)> Sent
        {
            get
            {
                lock (_sent)
                {
                    return _sent.ToList();
                }
            }
        }

        public void Clear()
        {
            lock (_sent)
            {
                _sent.Clear();
            }

            IsConfigured = true;
            Fails = false;
        }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (Fails)
            {
                throw new InvalidOperationException("The pretend mail server is down");
            }

            lock (_sent)
            {
                _sent.Add((to, subject, body));
            }

            return Task.CompletedTask;
        }
    }
}
