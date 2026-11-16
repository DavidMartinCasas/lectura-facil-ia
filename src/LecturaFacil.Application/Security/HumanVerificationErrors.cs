// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Security;

public static class HumanVerificationErrors
{
    public static readonly Error Failed = new(
        "human_verification.failed",
        "No hemos podido comprobar que eres una persona. Completa la verificación y vuelve a intentarlo.");
}
