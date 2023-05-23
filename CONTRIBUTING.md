# Contribution Guidelines

## Prerequisites

By contributing, you assert that:

- The contribution is your own original work.
- You have the right to assign the copyright for the work (it is not owned by your employer, or
  you have been given copyright assignment in writing).
- You [license](./LICENSE) the contribution under the terms applied to the rest of the project.
- You agree to follow the [code of conduct](./CODE_OF_CONDUCT.md).


## How to Contribute

### Where to find open issues
Generally you can find and work on open issues [here](https://github.com/Witteborn/GitHubBoilerplate/issues)

If something is on the issues list with the 'Up For Grabs' label or no one is asigned yet you can feel free to work on it. You should comment that you are signing up for it on the issue so someone else doesn't also sign up for the work.

If you find an issue that isn't contained in the list feel free to create one yourselfes, you wont need to work on it but expect further questions somewhere in the future.

### Set up your environment

 You create a branch based on the develop branch named with the following format: GH - ISSUE_NUMBER - SHORT_TITLE.
 - Please replace the ISSUE_NUMBER with the issue created for that pull request.
 - You can optionaly add a very short title for your issue so the git branches are a bit more easier to read.
 
 Example branch name:
 - GH-2-ContributionGuidelines
 - GH-2
 
Please do not change anything unrelated to that issue, if you think something unrelated should be changed open an issue for that thing in particular.
 

### Commits

A commit is a small logical unit that represents a change.
Please dont create large commits, they should be easy to review and only do what they describe.

Please start you commit messages with "(GH-ISSUE_NUMBER) My awesome message"
Example git message: "(GH-2) Adds contribution guidelines"


### Submit pull request
Prerequisites:

 - You are making commits in a seperate branch.
 - All code should compile without errors or warnings.
 - All tests should be passing.

Submitting PR:

Create a pull request(PR) of you branch against the develop branch.
The PR should be ready to merge, otherwise mark it as a draft.
Creating draft PRs help understanding you progress and means feedback can be given sooner. Waiting until you are ready may mean more changes than you are
interested in if the changes are taking things in a direction the maintainers do not want to go.

Please fill out the PR template, more detail is better and helps reviewing you changes.

One of the team members, or one of the maintainers, will evaluate it within a
reasonable time period. We are human and we have active lives outside of open source. Just know that we will evaluate your pull request.

### Respond to feedback on pull request

We may have feedback for you to fix or change some things. We generally like to see that pushed against the same topic branch (it will automatically update the Pull Request).

If we have comments or questions when we do evaluate it and receive no response, it will probably lessen the chance of getting accepted. Eventually, this means it will be closed if it is not accepted.
Please know this doesn't mean we don't value your contribution, just that things go stale. If in the future you want to pick it back up, feel free to address our concerns/questions/feedback and reopen the issue/open a new PR (referencing old one).

Sometimes we may need you to rebase your commit against the latest code before we can review it further.


## Other general information
If you reformat code or hit core functionality without an approval,
it's likely that no matter how awesome it looks afterwards, it will probably not get accepted.
Reformatting code makes it harder for us to evaluate exactly what was changed.

If you do these things, it will be make evaluation and acceptance easy.
Now if you stray outside of the guidelines we have above, it doesn't mean we are going to ignore
your pull request. It will just make things harder for us.
Harder for us roughly translates to a longer SLA for your pull request.


## Unclear
If anything is unclear please checkout on how we did certain thing in the past or feel free to open an issue based on the question template.


# Summary / TL;DR
- Open an issue before contributing
- Check for duplicates before creating an issue
- Each pull request has atleast one issue.
- Name your branch the same as the issue number: GH-2 for Issue with number 2.
- Name you pull request "(GH-IssueNumber) Title of your pull request"
- Include "(GH-IssueNumber)" at the beginning of each commit
- Your feature branch should be named after you issue aswell. Example: GH-2 or -GH-2-ContributionGuidelines
- The rule of thumb is ask if something is unclear before its too late


## Acknowledgement

This contribution guide contains pieces taken from the [Cake project](https://github.com/cake-build/cake) and was edited to be more flexible.