export interface LoginFormValues {
  userNameOrEmail: string;
  password: string;
  rememberMe: boolean;
}

export interface LoginFormErrors {
  userNameOrEmail?: string;
  password?: string;
}

export function validateLoginForm(values: LoginFormValues): LoginFormErrors {
  const errors: LoginFormErrors = {};

  const trimmedIdentifier = values.userNameOrEmail.trim();
  if (!trimmedIdentifier) {
    errors.userNameOrEmail = 'Vui lòng nhập Tên đăng nhập hoặc Email.';
  } else if (trimmedIdentifier.length > 256) {
    errors.userNameOrEmail = 'Tên đăng nhập hoặc Email không được vượt quá 256 ký tự.';
  }

  if (!values.password) {
    errors.password = 'Vui lòng nhập Mật khẩu.';
  } else if (values.password.length > 256) {
    errors.password = 'Mật khẩu không hợp lệ.';
  }

  return errors;
}

