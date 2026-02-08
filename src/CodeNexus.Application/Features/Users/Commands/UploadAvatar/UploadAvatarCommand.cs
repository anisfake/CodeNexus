using CodeNexus.Application.Common.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Commands.UploadAvatar
{
    public record UploadAvatarCommand(Stream ImageStream, string FileName) : IRequest<Result<string>>;
}
