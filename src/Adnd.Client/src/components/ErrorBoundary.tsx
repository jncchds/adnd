import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Alert, AlertTitle, Box, Button, Typography } from '@mui/material'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * Catches render-time crashes in a page so a single malformed payload shows a message
 * instead of blanking the whole app. Previously a combat event with an unexpected shape
 * threw inside the combat panel and left the user staring at a white screen.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled render error:', error, info.componentStack)
  }

  private reset = () => this.setState({ error: null })

  render() {
    const { error } = this.state
    if (!error) return this.props.children

    return (
      <Box sx={{ p: 3 }}>
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={this.reset}>Retry</Button>}
        >
          <AlertTitle>Something went wrong on this page</AlertTitle>
          <Typography variant="body2" sx={{ fontFamily: 'monospace', mt: 1 }}>
            {error.message}
          </Typography>
        </Alert>
      </Box>
    )
  }
}
