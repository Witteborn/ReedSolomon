# Contribution Guidelines

## Prerequisites

By contributing, you assert that:

- The contribution is your own original work.
- You have the right to assign the copyright for the work (it is not owned by your employer, or you have been given copyright assignment in writing).
- You [license](./LICENSE) the contribution under the terms applied to the rest of the project.
- You agree to follow the [code of conduct](./CODE_OF_CONDUCT.md).

## How to Contribute

### Where to Find Open Issues

Generally, you can find and work on open issues [here](https://github.com/Witteborn/GitHubBoilerplate/issues).

If an issue is labeled 'Up For Grabs' or is not assigned yet, feel free to work on it. You should comment that you are signing up for it on the issue so someone else doesn't also claim it.

If you find an issue that isn't listed, feel free to create one yourself. You won't need to work on it immediately, but expect further questions in the future.

### Set Up Your Environment

Create a branch based on the `develop` branch named with the following format: `GH-ISSUE_NUMBER-SHORT_TITLE`.

- Replace `ISSUE_NUMBER` with the issue number created for that pull request.
- You can optionally add a very short title for your issue so the Git branches are easier to read.

**Example branch names:**
- `GH-2-ContributionGuidelines`
- `GH-2`

Please do not change anything unrelated to that issue. If you think something unrelated should be changed, open an issue for that particular item.

### Commits

A commit is a small, logical unit that represents a change. Please don't create large commits; they should be easy to review and only do what they describe.

Please start your commit messages with `(GH-ISSUE_NUMBER) Your awesome message`.

**Example commit message:** `(GH-2) Add contribution guidelines`

### Submit a Pull Request

**Prerequisites:**

- You are making commits in a separate branch.
- All code should compile without errors or warnings.
- All tests should be passing.

**Submitting a PR:**

Create a pull request (PR) from your branch against the `develop` branch. The PR should be ready to merge; otherwise, mark it as a draft. Creating draft PRs helps us understand your progress and allows for feedback sooner. Waiting until you are ready may result in more changes than you are interested in if the changes take the project in a direction the maintainers do not want.

Please fill out the PR template. More detail is better and helps in reviewing your changes.

One of the team members or maintainers will evaluate it within a reasonable time period. We are human and have active lives outside of open source, but rest assured that we will evaluate your pull request.

### Respond to Feedback on Pull Requests

We may have feedback for you to fix or change some things. We generally prefer that these changes are pushed to the same topic branch, as it will automatically update the pull request.

If we have comments or questions and receive no response, it will probably lessen the chance of your PR being accepted. Eventually, this means it will be closed if it is not accepted. Please know this doesn't mean we don't value your contribution; it just means things have gone stale. If in the future you want to pick it back up, feel free to address our concerns, questions, or feedback and reopen the issue or open a new PR (referencing the old one).

Sometimes we may need you to rebase your commits against the latest code before we can review them further.

## Other General Information

If you reformat code or alter core functionality without approval, it's likely that, no matter how awesome it looks afterward, it will not get accepted. Reformatting code makes it harder for us to evaluate exactly what was changed.

If you follow these guidelines, it will make evaluation and acceptance easier. Straying outside the guidelines above doesn't mean we will ignore your pull request, but it will make things harder for us. "Harder for us" roughly translates to a longer SLA for your pull request.

## Unclear

If anything is unclear, please check how we have handled similar situations in the past or feel free to open an issue using the question template.

# Summary / TL;DR

- Open an issue before contributing.
- Check for duplicates before creating an issue.
- Each pull request must have at least one issue.
- Name your branch the same as the issue number: `GH-2` for an issue with number 2.
- Name your pull request `(GH-IssueNumber) Title of your pull request`.
- Include `(GH-IssueNumber)` at the beginning of each commit.
- Your feature branch should be named after your issue as well. Example: `GH-2` or `GH-2-ContributionGuidelines`.
- The rule of thumb is to ask if something is unclear before it's too late.

## Acknowledgement

This contribution guide contains pieces taken from the [Cake project](https://github.com/cake-build/cake) and was edited to be more flexible.
