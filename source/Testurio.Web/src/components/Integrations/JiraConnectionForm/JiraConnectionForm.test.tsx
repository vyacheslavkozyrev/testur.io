/**
 * Feature 0024 — Automatic Work Item Status Transition
 * Tests that JiraConnectionForm renders and submits transition status fields.
 */
import '@/i18n';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import JiraConnectionForm from './JiraConnectionForm';
import type { SaveJiraConnectionRequest } from '@/types/pmTool.types';

const noop = () => {};

function renderForm(
  onSubmit: (data: SaveJiraConnectionRequest) => void = noop,
  isSubmitting = false,
) {
  return render(
    <JiraConnectionForm isSubmitting={isSubmitting} onSubmit={onSubmit} />,
  );
}

/** Fill in the required fields of the Jira form. */
async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Base URL/i), 'https://myorg.atlassian.net');
  await user.type(screen.getByLabelText(/Project Key/i), 'PROJ');
  await user.type(screen.getByLabelText(/"In Testing" Status Name/i), 'In Testing');
  await user.type(screen.getByLabelText(/Email Address/i), 'user@example.com');
  // API Token label appears once when auth method is apiToken (default).
  await user.type(screen.getByLabelText('API Token *'), 'mytoken');
}

describe('JiraConnectionForm — transition status fields', () => {
  it('renders the Passed transition status field', () => {
    renderForm();
    expect(screen.getByLabelText(/Passed — Transition To Status/i)).toBeInTheDocument();
  });

  it('renders the Failed transition status field', () => {
    renderForm();
    expect(screen.getByLabelText(/Failed — Transition To Status/i)).toBeInTheDocument();
  });

  it('transition fields are optional — form submits without them', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveJiraConnectionRequest;
    expect(submitted.passedTransitionStatus).toBeUndefined();
    expect(submitted.failedTransitionStatus).toBeUndefined();
  });

  it('submits passedTransitionStatus when filled in', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Passed — Transition To Status/i), 'Done');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveJiraConnectionRequest;
    expect(submitted.passedTransitionStatus).toBe('Done');
  });

  it('submits failedTransitionStatus when filled in', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Failed — Transition To Status/i), 'Rejected');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveJiraConnectionRequest;
    expect(submitted.failedTransitionStatus).toBe('Rejected');
  });

  it('submits both transition fields when both are filled', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Passed — Transition To Status/i), 'Done');
    await user.type(screen.getByLabelText(/Failed — Transition To Status/i), 'Rejected');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveJiraConnectionRequest;
    expect(submitted.passedTransitionStatus).toBe('Done');
    expect(submitted.failedTransitionStatus).toBe('Rejected');
  });

  it('renders Cancel button when onCancel is provided', () => {
    render(
      <JiraConnectionForm isSubmitting={false} onSubmit={noop} onCancel={noop} />,
    );
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('does not render Cancel button when onCancel is not provided', () => {
    renderForm();
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
  });

  it('disables Save button while submitting', () => {
    renderForm(noop, true);
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
  });
});
