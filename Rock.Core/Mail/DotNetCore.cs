#if !NET45
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Rock.Mail
{
    public class MailAddress
    {
        public MailAddress(string address)
        {
            Address = address;
        }

        public MailAddress(string address, string displayName)
        {
            Address = address;
            DisplayName = displayName;
        }

        public string Address { get; }
        public string DisplayName { get; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(DisplayName))
            {
                return Address;
            }

            return $"\"{DisplayName}\" {Address}";
        }
    }

    public class MailAddressCollection : Collection<MailAddress>
    {
        public override string ToString()
        {
            return string.Join(", ", this);
        }
    }

    public class MailMessage
    {
        public MailMessage(string from, string to)
            : this(new MailAddress(from), new MailAddress(to))
        {
        }

        public MailMessage(MailAddress from, MailAddress to)
        {
            From = from;
            To.Add(to);
        }

        public MailMessage(string from, string to, string subject, string body)
            : this(new MailAddress(from), new MailAddress(to))
        {
            Subject = subject;
            Body = body;
        }

        public MailAddress From { get; }
        public MailAddressCollection To { get; } = new MailAddressCollection();
        public MailAddressCollection CC { get; } = new MailAddressCollection();
        public MailAddressCollection Bcc { get; } = new MailAddressCollection();

        public MailAddress Sender { get; set; }
        public MailAddressCollection ReplyToList { get; }

        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsBodyHtml { get; set; }
    }

    public enum SmtpDeliveryMethod
    {
        Network,
        SpecifiedPickupDirectory
    }

    public class SmtpClient : IDisposable
    {
        private readonly Lazy<Func<MimeMessage, Task>> _sendAsync;
        private readonly Lazy<Action<MimeMessage>> _send;
        private readonly Lazy<Action> _dispose;

        public SmtpClient()
        {
            var client = new Lazy<MailKit.Net.Smtp.SmtpClient>(() =>
            {
                var c = new MailKit.Net.Smtp.SmtpClient();

                if (Host == null)
                {
                    // TODO: get host from "somewhere"
                }

                c.Connect(Host, Port, false);

                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                c.AuthenticationMechanisms.Remove("XOAUTH2");

                return c;
            });

            _sendAsync = new Lazy<Func<MimeMessage, Task>>(() =>
            {
                switch (DeliveryMethod)
                {
                    case SmtpDeliveryMethod.Network:
                        return message => client.Value.SendAsync(message);
                    case SmtpDeliveryMethod.SpecifiedPickupDirectory:
                        return message => Task.CompletedTask;
                    default:
                        throw new InvalidOperationException();
                }
            });

            _send = new Lazy<Action<MimeMessage>>(() =>
            {
                switch (DeliveryMethod)
                {
                    case SmtpDeliveryMethod.Network:
                        return message => client.Value.Send(message);
                    case SmtpDeliveryMethod.SpecifiedPickupDirectory:
                        return message => { };
                    default:
                        throw new InvalidOperationException();
                }
            });

            _dispose = new Lazy<Action>(() =>
            {
                switch (DeliveryMethod)
                {
                    case SmtpDeliveryMethod.Network:
                        return () => client.Value.Dispose();
                    case SmtpDeliveryMethod.SpecifiedPickupDirectory:
                        return () => { };
                    default:
                        throw new InvalidOperationException();
                }
            });
        }

        public SmtpDeliveryMethod DeliveryMethod { get; set; }

        public string Host { get; set; }
        public int Port { get; set; }

        public string PickupDirectoryLocation { get; set; }

        public Task SendMailAsync(MailMessage mailMessage)
        {
            return _sendAsync.Value(GetMimeMessage(mailMessage));
        }

        public void Send(MailMessage mailMessage)
        {
            _send.Value(GetMimeMessage(mailMessage));
        }

        void IDisposable.Dispose()
        {
            _dispose.Value();
        }

        private static MimeMessage GetMimeMessage(MailMessage mailMessage)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(mailMessage.From.DisplayName, mailMessage.From.Address));

            foreach (var toAddress in mailMessage.To)
            {
                message.To.Add(new MailboxAddress(toAddress.DisplayName, toAddress.Address));
            }

            foreach (var ccAddress in mailMessage.CC)
            {
                message.Cc.Add(new MailboxAddress(ccAddress.DisplayName, ccAddress.Address));
            }

            foreach (var bccAddress in mailMessage.Bcc)
            {
                message.Bcc.Add(new MailboxAddress(bccAddress.DisplayName, bccAddress.Address));
            }

            message.Subject = mailMessage.Subject;

            message.Body = new TextPart(mailMessage.IsBodyHtml ? TextFormat.Html : TextFormat.Plain)
            {
                Text = mailMessage.Body
            };

            return message;
        }
    }
}
#endif
