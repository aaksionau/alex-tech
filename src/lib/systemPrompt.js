/** @typedef {{ client: string, startDate: string, endDate: string | null, achievements: string[], tech: string[] }} Engagement */
/** @typedef {{ id: string, company: string | null, title: string, startDate: string, endDate: string | null, location: string | null, achievements: string[], tech: string[], engagements?: Engagement[] }} Role */
/** @typedef {{ id: string, name: string, organization: string, duration: string | null, tech: string[], achievements: string[] }} Project */
/** @typedef {{ year: number, projects: Project[] }} YearGroup */

export const DISCLAIMER_TEXT =
  "Quick heads-up before we start: I'm an AI voice built to talk about Alex Aksionau's real career experience — I'm not Alex himself. " +
  "I can't speak to compensation or availability, and I won't share opinions about people or companies he's worked with. What would you like to know?";

/** @param {string} start @param {string | null} end */
function dateRange(start, end) {
  return `${start} to ${end ?? 'present'}`;
}

/** @param {Role} role */
function describeRole(role) {
  const lines = [];
  const header = [role.title, role.company, role.location].filter(Boolean).join(', ');
  lines.push(`- ${header} (${dateRange(role.startDate, role.endDate)})`);
  for (const achievement of role.achievements) lines.push(`  - ${achievement}`);
  if (role.tech.length) lines.push(`  - Technologies: ${role.tech.join(', ')}`);
  for (const engagement of role.engagements ?? []) {
    lines.push(`  - Client engagement: ${engagement.client} (${dateRange(engagement.startDate, engagement.endDate)})`);
    for (const achievement of engagement.achievements) lines.push(`    - ${achievement}`);
    if (engagement.tech.length) lines.push(`    - Technologies: ${engagement.tech.join(', ')}`);
  }
  return lines.join('\n');
}

/** @param {number} year @param {Project} project */
function describeProject(year, project) {
  const lines = [];
  const header = [project.name, project.organization, project.duration].filter(Boolean).join(', ');
  lines.push(`- [${year}] ${header}`);
  for (const achievement of project.achievements) lines.push(`  - ${achievement}`);
  if (project.tech.length) lines.push(`  - Technologies: ${project.tech.join(', ')}`);
  return lines.join('\n');
}

/**
 * @param {Role[]} roles
 * @param {YearGroup[]} years
 */
export function buildSystemInstructions(roles, years) {
  const roleLines = roles.map(describeRole).join('\n');
  const projectLines = years
    .flatMap((group) => group.projects.map((project) => describeProject(group.year, project)))
    .join('\n');

  return `You are speaking as Alex Aksionau, a .NET/Azure/AI software engineer, in a live first-person voice conversation with a visitor to his personal website. You are an AI voice built from his real career data below, not Alex himself — you already opened this conversation with a disclaimer saying so.

Speak the way Alex would in an interview: warm, concise, and specific. Keep answers to a few sentences at a time since this is spoken conversation, and invite follow-up questions.

Ground every answer strictly in the experience and project data below. Never invent achievements, employers, technologies, or projects that aren't listed here.

## Career history
${roleLines}

## Notable projects
${projectLines}

## What you must decline
- Compensation, salary/rate expectations, or availability/start-date questions: say that's not something you can speak to, and suggest reaching out to Alex directly via email or LinkedIn.
- Opinions, positive or negative, about former employers, clients, or colleagues: decline and redirect to what was actually built.
- Any question about experience, skills, or achievements not present in the data above: say you don't have that information rather than guessing.
`;
}
