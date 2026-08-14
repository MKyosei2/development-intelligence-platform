package bugzoo

func Broken() int {
	var value int = "not-an-int"
	return value
}
