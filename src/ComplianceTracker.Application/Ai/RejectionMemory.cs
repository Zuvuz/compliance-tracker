using System.Globalization;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Ai
{
    // US-145: what a rejection was about, and the note a suggestion carries when it comes back after a change
    public static class RejectionMemory
    {
        // R3: the text of a requirement (its code, title and description)
        public static string Of(Requirement requirement) => Fingerprint.Of(requirement.Code, requirement.Title, requirement.Description);

        // R3: a story's or product requirement's title and text
        public static string Of(WorkItem story) => Fingerprint.Of(story.Title, story.MatchingText);

        // R3: evidence: its file (SHA-256), or its title and link
        public static string Of(Evidence evidence) => evidence.Sha256 ?? Fingerprint.Of(evidence.Title, evidence.Url);

        // R3: an organisation document: its current file
        public static string Of(OrganisationDocument document) => document.Sha256;

        public static string Of(Risk risk) => Fingerprint.Of(risk.Title, risk.Description);

        // R4: e.g. "Rejected before by Bo on 03-11-2026 (NotRelevant). The evidence has changed since."
        public static string? Note(string? rejectedFingerprint, string current, string decidedBy, DateTime? decidedAt, RejectionReason? reason, string item)
        {
            var changed = Fingerprint.WhatChanged(rejectedFingerprint, current);
            if (changed is null)
            {
                return null;
            }

            var what = changed switch
            {
                "item" => $"The {item} has changed since.",
                "requirement" => "The requirement's text has changed since.",
                _ => $"The {item} and the requirement's text have changed since."
            };
            var when = decidedAt?.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "?";
            return $"Rejected before by {decidedBy} on {when}{(reason is null ? "" : $" ({reason})")}. {what}";
        }
    }
}
