/**
 * Feature 0024 — Automatic Work Item Status Transition
 * Tests that ADOConnectionForm renders and submits transition status fields.
 */
import '@/i18n';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ADOConnectionForm from './ADOConnectionForm';
import type { SaveADOConnectionRequest } from '@/types/pmTool.types';

const noop = () => {};

function renderForm(
  onSubmit: (data: SaveADOConnectionRequest) => void = noop,
  isSubmitting = false,
) {
  return render(
    <ADOConnectionForm isSubmitting={isSubmitting} onSubmit={onSubmit} />,
  );
}

/** Fill in the required fields of the ADO form. */
async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Organization URL/i), 'https://dev.azure.com/myorg');
  await user.type(screen.getByLabelText(/Project Name/i), 'My Project');
  await user.type(screen.getByLabelText(/^Team \*/i), 'My Team');
  await user.type(screen.getByLabelText(/"In Testing" Status Name/i), 'In Testing');
  // PAT label appears once when auth method is PAT (default).
  await user.type(screen.getByLabelText('Personal Access Token *'), 'mypat');
}

describe('ADOConnectionForm — transition status fields', () => {
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

    const submitted = onSubmit.mock.calls[0][0] as SaveADOConnectionRequest;
    expect(submitted.passedTransitionStatus).toBeUndefined();
    expect(submitted.failedTransitionStatus).toBeUndefined();
  });

  it('submits passedTransitionStatus when filled in', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Passed — Transition To Status/i), 'Closed');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveADOConnectionRequest;
    expect(submitted.passedTransitionStatus).toBe('Closed');
  });

  it('submits failedTransitionStatus when filled in', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Failed — Transition To Status/i), 'Active');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveADOConnectionRequest;
    expect(submitted.failedTransitionStatus).toBe('Active');
  });

  it('submits both transition fields when both are filled', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    renderForm(onSubmit);

    await fillRequired(user);
    await user.type(screen.getByLabelText(/Passed — Transition To Status/i), 'Closed');
    await user.type(screen.getByLabelText(/Failed — Transition To Status/i), 'Active');

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    const submitted = onSubmit.mock.calls[0][0] as SaveADOConnectionRequest;
    expect(submitted.passedTransitionStatus).toBe('Closed');
    expect(submitted.failedTransitionStatus).toBe('Active');
  });

  it('renders Cancel button when onCancel is provided', () => {
    render(
      <ADOConnectionForm isSubmitting={false} onSubmit={noop} onCancel={noop} />,
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
